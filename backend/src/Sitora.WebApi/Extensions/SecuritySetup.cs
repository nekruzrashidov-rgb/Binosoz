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
    // Заявки застройщиков с /register: каждую рассматривает человек, поэтому лимит строже.
    public int ApplicationsPerHour { get; set; } = 3;

    // AI-помощник: каждое сообщение — платный запрос к Claude, поэтому лимит на пользователя.
    public int AiPerHour { get; set; } = 60;

    // Общий лимит на все запросы: гости — на IP, вошедшие — на пользователя
    // (сотрудники за одним офисным IP не мешают друг другу).
    public int AnonymousPerMinute { get; set; } = 300;
    public int UserPerMinute { get; set; } = 600;
}

public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Sms = "sms";
    // Заявки и брони без входа; сотрудников не ограничиваем.
    public const string PublicForms = "public-forms";
    public const string Applications = "applications";
    public const string Ai = "ai";
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
            // Свой счётчик, отдельный от заявок на квартиры и броней.
            options.AddPolicy(RateLimitPolicies.Applications, context =>
                PerIp(context, s => s.ApplicationsPerHour, TimeSpan.FromHours(1), "applications:"));

            options.AddPolicy(RateLimitPolicies.Ai, context =>
            {
                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
                var userId = context.User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub)?.Value;
                return FixedWindow("ai:" + (userId ?? ClientIp(context)), settings.AiPerHour, TimeSpan.FromHours(1));
            });

            // Общий лимит действует вместе с политиками выше; проверки здоровья не ограничиваем.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                if (context.Request.Path.StartsWithSegments("/health"))
                    return RateLimitPartition.GetNoLimiter("health");

                var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
                var userId = context.User.FindFirst(Microsoft.IdentityModel.JsonWebTokens.JwtRegisteredClaimNames.Sub)?.Value;
                return userId is not null
                    ? FixedWindow("user:" + userId, settings.UserPerMinute, TimeSpan.FromMinutes(1))
                    : FixedWindow("ip:" + ClientIp(context), settings.AnonymousPerMinute, TimeSpan.FromMinutes(1));
            });
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

    private static RateLimitPartition<string> PerIp(HttpContext context, Func<RateLimitSettings, int> limit, TimeSpan window, string prefix = "")
    {
        var settings = context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;
        return FixedWindow(prefix + ClientIp(context), limit(settings), window);
    }

    private static RateLimitPartition<string> FixedWindow(string key, int permitLimit, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0
        });

    private static string ClientIp(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
