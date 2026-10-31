using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Sitora.Infrastructure.Services;

// Кэш поверх IDistributedCache (Redis или память) со сбросом «всё разом». Сбросить все записи в IDistributedCache
// нельзя, поэтому ключи живут внутри «поколения»: {приставка}:{поколение}:{ключ}. InvalidateAsync() записывает новое
// поколение, и старые записи больше не читаются (сами истекут по сроку). Поколение — случайное значение, а не счётчик:
// у IDistributedCache нет атомарного увеличения, и два одновременных «прочитал 5 — записал 6» потеряли бы один сброс.
// Если кэш недоступен (Redis упал), данные читаются из базы напрямую.
public abstract class GenerationCache(IDistributedCache cache, ILogger logger, string prefix, TimeSpan lifetime)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string GenerationKey => prefix + ":gen";

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, CancellationToken ct = default)
    {
        string scope;
        try
        {
            scope = await ScopeAsync(ct);
            var cached = await cache.GetAsync(EntryKey(scope, key), ct);
            if (cached is not null)
                return JsonSerializer.Deserialize<T>(cached, Json)!;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Кэш {Prefix} недоступен, читаем из базы", prefix);
            return await factory();
        }

        var value = await factory();
        try
        {
            // Если данные изменились, пока мы читали базу, прочитанное могло устареть: не кладём его в кэш.
            if (await ScopeAsync(ct) == scope)
                await cache.SetAsync(EntryKey(scope, key), JsonSerializer.SerializeToUtf8Bytes(value, Json),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime }, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Не удалось записать в кэш {Prefix}", prefix);
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
            // Записи без сброса проживут не дольше своего срока.
            logger.LogWarning(e, "Не удалось сбросить кэш {Prefix}", prefix);
        }
    }

    // Текущее поколение. Нет его (первый запуск или Redis очищен) — заводим своё; если два экземпляра заведут разные,
    // будет лишь один лишний промах.
    public async Task<string> GenerationAsync(CancellationToken ct = default)
    {
        var generation = await cache.GetStringAsync(GenerationKey, ct);
        if (generation is not null)
            return generation;
        generation = NewGeneration();
        await cache.SetStringAsync(GenerationKey, generation, ct);
        return generation;
    }

    // Часть ключа, при смене которой все записи устаревают; наследник может добавить поколения других кэшей.
    protected virtual Task<string> ScopeAsync(CancellationToken ct) => GenerationAsync(ct);

    private static string NewGeneration() => Guid.NewGuid().ToString("N");

    private string EntryKey(string scope, string key) => $"{prefix}:{scope}:{key}";
}
