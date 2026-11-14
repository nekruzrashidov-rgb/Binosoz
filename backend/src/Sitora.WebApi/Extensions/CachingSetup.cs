using Microsoft.AspNetCore.OutputCaching;
using Sitora.Infrastructure.Integrations;

namespace Sitora.WebApi.Extensions;

// Кэш ответов (Output Caching) для публичных GET без входа: каталог, отзывы, бренд, курс.
// Ключ — путь, вся строка запроса и Accept-Language. Запросы с Authorization и ответы с Set-Cookie
// не кэшируются никогда (правило политики по умолчанию): персональные данные в кэш не попадают.
public static class CachingSetup
{
    // Каталог: дома, этажи, квартиры. Сбрасывается тегом при любом изменении каталога, статуса, брони, цены (CatalogCache).
    public const string Catalog = "catalog";
    // Отзывы, бренд, курс: меняются редко, живут 15 секунд без сброса.
    public const string PublicShort = "public-short";
    // Публичные отзывы: сбрасываются тегом при ответе, публикации и удалении (ReviewService).
    public const string Reviews = "reviews";
    // «Зелёный эффект» на главной: счётчики меняются медленно, живут 5 минут.
    public const string GreenImpact = "green-impact";
    public static readonly TimeSpan GreenImpactLifetime = TimeSpan.FromMinutes(5);
    public const string CatalogTag = Sitora.Infrastructure.Services.CatalogCache.OutputCacheTag;

    public static readonly TimeSpan CatalogLifetime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ShortLifetime = TimeSpan.FromSeconds(15);
    public const int BrowserMaxAgeSeconds = 15;

    public static IServiceCollection AddSitoraOutputCache(this IServiceCollection services, IConfiguration configuration)
    {
        // Несколько экземпляров API делят кэш через Redis; без Redis — память процесса.
        var redis = configuration[$"{RedisOptions.SectionName}:{nameof(RedisOptions.ConnectionString)}"];
        if (!string.IsNullOrWhiteSpace(redis))
            services.AddStackExchangeRedisOutputCache(o =>
            {
                o.Configuration = redis;
                o.InstanceName = (configuration[$"{RedisOptions.SectionName}:{nameof(RedisOptions.InstanceName)}"] ?? "binosoz:") + "out:";
            });

        services.AddOutputCache(o =>
        {
            o.AddPolicy(Catalog, b => b.Expire(CatalogLifetime).SetVaryByQuery("*").SetVaryByHeader("Accept-Language").Tag(CatalogTag));
            o.AddPolicy(PublicShort, b => b.Expire(ShortLifetime).SetVaryByQuery("*").SetVaryByHeader("Accept-Language"));
            o.AddPolicy(GreenImpact, b => b.Expire(GreenImpactLifetime).SetVaryByQuery("*"));
            o.AddPolicy(Reviews, b => b.Expire(CatalogLifetime).SetVaryByQuery("*").SetVaryByHeader("Accept-Language").Tag(Sitora.Infrastructure.Services.ReviewService.OutputCacheTag));
        });
        return services;
    }

    // Cache-Control для ответов API: публичные кэшируемые — public, max-age=15 (браузер и CDN держат их недолго);
    // всё, что получено со входом, — private, no-store (ни браузер, ни прокси не сохраняют персональные данные).
    public static IApplicationBuilder UseSitoraCacheHeaders(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        if (FrontendSetup.IsApiPath(context.Request.Path) && !context.Request.Path.StartsWithSegments("/health"))
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                if (!string.IsNullOrEmpty(headers.CacheControl))
                    return Task.CompletedTask;
                var authenticated = context.Request.Headers.Authorization.Count > 0 || context.User.Identity?.IsAuthenticated == true;
                var cacheable = !authenticated && HttpMethods.IsGet(context.Request.Method) && context.Response.StatusCode == StatusCodes.Status200OK
                    && context.GetEndpoint()?.Metadata.GetMetadata<OutputCacheAttribute>() is not null;
                headers.CacheControl = cacheable ? $"public, max-age={BrowserMaxAgeSeconds}" : "private, no-store";
                return Task.CompletedTask;
            });
        }
        return next(context);
    });
}
