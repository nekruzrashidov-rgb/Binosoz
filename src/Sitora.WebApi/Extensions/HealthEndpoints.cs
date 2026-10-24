using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sitora.Infrastructure.Health;

namespace Sitora.WebApi.Extensions;

public static class HealthEndpoints
{
    // /health/live — процесс отвечает (для проверки контейнера), зависимости не трогает.
    // /health/ready и /health — база, хранилище файлов и, где включён, сервер фоновых задач.
    public static void MapSitoraHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteAsync });
        var ready = new HealthCheckOptions { Predicate = c => c.Tags.Contains(HealthTags.Ready), ResponseWriter = WriteAsync };
        app.MapHealthChecks("/health/ready", ready);
        app.MapHealthChecks("/health", ready);
    }

    // Только имена и статусы проверок: текст ошибок и исключения наружу не отдаются, они есть в логе.
    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        });
}

// Режим `dotnet Sitora.WebApi.dll --healthcheck` для HEALTHCHECK контейнера: в образе нет curl.
// Запрашивает /health/live у уже запущенного API и выходит с кодом 0 или 1.
public static class HealthProbe
{
    public const string Command = "--healthcheck";

    public static async Task<int> RunAsync()
    {
        var port = (Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS") ?? "8080").Split(';', ',')[0].Trim();
        // В продакшене API принимает только свой домен (AllowedHosts), поэтому подставляем его в Host.
        var host = (Environment.GetEnvironmentVariable("AllowedHosts") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(h => h != "*" && !h.StartsWith("*.")) ?? "localhost";

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/health/live");
        request.Headers.Host = host;
        try
        {
            using var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
