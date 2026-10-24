using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Sitora.WebApi.Extensions;

public class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    // Лимиты на один IP-адрес.
    public int LoginPerMinute { get; set; } = 10;
    public int SmsPerHour { get; set; } = 20;
    public int PublicFormsPerHour { get; set; } = 30;
}

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Sms = "sms";
    // Заявки и брони без входа; сотрудников не ограничиваем.
    public const string PublicForms = "public-forms";
}

public static class SecuritySetup
{
    public const string CorsPolicy = "frontend";

    public static IServiceCollection AddSitoraRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitSettings>().BindConfiguration(RateLimitSettings.SectionName);

        services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, _) =>
            {
                var response = context.HttpContext.Response;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();

                response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Слишком много запросов",
                        Detail = "Слишком много запросов. Попробуйте позже"
                    }
                });
            };

            options.AddPolicy(RateLimitPolicies.Login, context =>
                PerIp(context, s => s.LoginPerMinute, TimeSpan.FromMinutes(1)));
            options.AddPolicy(RateLimitPolicies.Sms, context =>
                PerIp(context, s => s.SmsPerHour, TimeSpan.FromHours(1)));
            options.AddPolicy(RateLimitPolicies.PublicForms, context =>
                context.User.Identity?.IsAuthenticated == true
                    ? RateLimitPartition.GetNoLimiter("staff")
                    : PerIp(context, s => s.PublicFormsPerHour, TimeSpan.FromHours(1)));
        });

        return services;
    }

    // Список доменов фронтенда задаётся в Cors:AllowedOrigins; пустой список — CORS выключен.
    public static IServiceCollection AddSitoraCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((options, configuration) =>
        {
            var origins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .ToArray();
            options.AddPolicy(CorsPolicy, policy =>
            {
                if (origins.Length > 0)
                    // Клиент SignalR в браузере отправляет negotiate с credentials.
                    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
            });
        });
        return services;
    }

    // Включать только когда API доступен лишь через свой обратный прокси (Caddy в docker-compose.prod.yml):
    // иначе любой клиент сможет подставить себе IP и обойти лимиты.
    public static IServiceCollection AddSitoraForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

    private static RateLimitPartition<string> PerIp(HttpContext context, Func<RateLimitSettings, int> limit, TimeSpan window)
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit(settings),
            Window = window,
            QueueLimit = 0
        });
    }
}
