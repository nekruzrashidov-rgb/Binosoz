using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Sitora.Infrastructure.Services;

// Кэш публичного каталога на 30 секунд. Любое изменение каталога или статуса квартиры вызывает InvalidateAsync().
// Он же сбрасывает кэш HTTP-ответов каталога (Output Caching, тег «catalog»), если API его подключил.
public class CatalogCache(IDistributedCache cache, ILogger<CatalogCache> logger, IServiceProvider? services = null)
    : GenerationCache(cache, logger, "catalog", TimeSpan.FromSeconds(30))
{
    public const string OutputCacheTag = "catalog";

    public override async Task InvalidateAsync(CancellationToken ct = default)
    {
        await base.InvalidateAsync(ct);
        var store = services?.GetService<IOutputCacheStore>();
        if (store is null)
            return;
        try
        {
            await store.EvictByTagAsync(OutputCacheTag, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Ответы без сброса проживут не дольше своего срока (30 секунд).
            logger.LogWarning(e, "Не удалось сбросить кэш ответов каталога");
        }
    }
}

// Статистика CRM на 5 минут. Её ключи зависят и от поколения каталога: бронь, сделка или правка каталога меняют
// статусы квартир и сбрасывают статистику сами. Отдельно её сбрасывают платежи, графики, новые заявки и правки сделок.
public class StatsCache(IDistributedCache cache, CatalogCache catalog, ILogger<StatsCache> logger)
    : GenerationCache(cache, logger, "stats", TimeSpan.FromMinutes(5))
{
    protected override async Task<string> ScopeAsync(CancellationToken ct) =>
        await catalog.GenerationAsync(ct) + ":" + await GenerationAsync(ct);
}
