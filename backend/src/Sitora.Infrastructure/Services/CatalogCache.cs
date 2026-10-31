using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Sitora.Infrastructure.Services;

// Кэш публичного каталога на 30 секунд. Любое изменение каталога или статуса квартиры вызывает InvalidateAsync().
public class CatalogCache(IDistributedCache cache, ILogger<CatalogCache> logger)
    : GenerationCache(cache, logger, "catalog", TimeSpan.FromSeconds(30));

// Статистика CRM на 5 минут. Её ключи зависят и от поколения каталога: бронь, сделка или правка каталога меняют
// статусы квартир и сбрасывают статистику сами. Отдельно её сбрасывают платежи, графики, новые заявки и правки сделок.
public class StatsCache(IDistributedCache cache, CatalogCache catalog, ILogger<StatsCache> logger)
    : GenerationCache(cache, logger, "stats", TimeSpan.FromMinutes(5))
{
    protected override async Task<string> ScopeAsync(CancellationToken ct) =>
        await catalog.GenerationAsync(ct) + ":" + await GenerationAsync(ct);
}
