using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sitora.Infrastructure;
using Sitora.Infrastructure.Health;

namespace Sitora.WebApi.Extensions;

public static class HealthEndpoints
{
    // /health/live — процесс отвечает (для проверки контейнера), зависимости не трогает.
    // /health/ready и /health — база, хранилище файлов и, где включён, сервер фоновых задач.
    public const string KeyHeader = "X-Health-Key";

    // /health/live — публичный (Docker, Caddy, Render, uptime-мониторинг): статус и версия, без подробностей.
    // /health/ready и /health: список проверок (база, хранилище, почта…) — только с заголовком X-Health-Key = Health__Key
    // или, вне Production, из внутренней сети (loopback, частные адреса). В Production адрес не в счёт: на Render все запросы
    // приходят через прокси с внутреннего адреса. Остальным — только общий статус { status }, без checks.
    private const string BriefItem = "sitora.health.brief";

    public static void MapSitoraHealthChecks(this WebApplication app)
    {
        var key = app.Configuration["Health:Key"];
        var production = app.Environment.IsProduction();
        app.UseWhen(http => http.Request.Path.StartsWithSegments("/health") && !http.Request.Path.StartsWithSegments("/health/live"), branch =>
            branch.Use((http, next) =>
            {
                var full = KeyMatches(http.Request.Headers[KeyHeader], key) || (!production && IsInternal(http.Connection.RemoteIpAddress));
                if (!full)
                    http.Items[BriefItem] = true;
                return next(http);
            }));

        // Живой процесс и его версия (короткий SHA коммита, время сборки) — без проверок зависимостей.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = WriteLiveAsync });
        var ready = new HealthCheckOptions { Predicate = c => c.Tags.Contains(HealthTags.Ready), ResponseWriter = WriteAsync };
        app.MapHealthChecks("/health/ready", ready);
        app.MapHealthChecks("/health", ready);
    }

    // Запрос изнутри: TestServer (адреса нет), loopback, частные сети (docker, VPS), link-local.
    public static bool IsInternal(System.Net.IPAddress? ip)
    {
        if (ip is null || System.Net.IPAddress.IsLoopback(ip))
            return true;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        var b = ip.GetAddressBytes();
        return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254)
            : ip.IsIPv6LinkLocal || ip.IsIPv6UniqueLocal;
    }

    private static bool KeyMatches(string? given, string? expected) =>
        !string.IsNullOrEmpty(expected) && given is not null
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(given), System.Text.Encoding.UTF8.GetBytes(expected));

    private static Task WriteLiveAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), version = BuildInfo.Version, builtAt = BuildInfo.BuiltAt });

    // Имена, статусы и наши собственные пояснения проверок («Почта: Gmail настроен», «Файлы хранятся на временном
    // диске…»); исключения и их текст наружу не отдаются, они есть в логе. Без ключа (в Production) — только статус.
    private static Task WriteAsync(HttpContext context, HealthReport report) =>
        context.Items.ContainsKey(BriefItem)
            ? context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() })
            : context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
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
