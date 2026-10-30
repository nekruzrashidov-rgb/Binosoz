using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Sitora.Infrastructure.Services;

// Кэш публичного каталога на 30 секунд — в Redis (общий для всех экземпляров API) или в памяти.
// Сбросить все записи разом в IDistributedCache нельзя, поэтому ключи живут внутри «поколения»:
// catalog:{поколение}:{ключ}. InvalidateAsync() записывает новое поколение, и старые записи больше не читаются
// (они сами истекут через 30 секунд). Поколение — случайное значение, а не счётчик: у IDistributedCache нет
// атомарного увеличения, и два одновременных «прочитал 5 — записал 6» потеряли бы один сброс.
// Если кэш недоступен (Redis упал), каталог читается из базы напрямую.
public class CatalogCache(IDistributedCache cache, ILogger<CatalogCache> logger)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private const string GenerationKey = "catalog:gen";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct = default)
    {
        string generation;
        try
        {
            generation = await GetGenerationAsync(ct);
            var cached = await cache.GetAsync(EntryKey(generation, key), ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<T>(cached, Json)!;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Кэш каталога недоступен, читаем из базы");
            return await factory();
        }

        var value = await factory();
        try
        {
            // Если каталог изменился, пока мы читали базу, данные могли устареть: не кладём их в кэш.
            if (await GetGenerationAsync(ct) == generation)
                await cache.SetAsync(EntryKey(generation, key), JsonSerializer.SerializeToUtf8Bytes(value, Json),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Не удалось записать каталог в кэш");
        }

        return value;
    }

    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        try
        {
            await cache.SetStringAsync(GenerationKey, NewGeneration(), ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Записи без сброса проживут не дольше 30 секунд.
            logger.LogWarning(e, "Не удалось сбросить кэш каталога");
        }
    }

    // Нет поколения (первый запуск или Redis очищен) — заводим своё; если два экземпляра заведут разные,
    // будет лишь один лишний промах.
    private async Task<string> GetGenerationAsync(CancellationToken ct)
    {
        var generation = await cache.GetStringAsync(GenerationKey, ct);
        if (generation is not null)
            return generation;
        generation = NewGeneration();
        await cache.SetStringAsync(GenerationKey, generation, ct);
        return generation;
    }

    private static string NewGeneration() => Guid.NewGuid().ToString("N");

    private static string EntryKey(string generation, string key) => $"catalog:{generation}:{key}";
}
