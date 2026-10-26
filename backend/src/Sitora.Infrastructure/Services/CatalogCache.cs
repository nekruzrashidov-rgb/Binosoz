using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Sitora.Infrastructure.Services;

// Кэш публичного каталога на 30 секунд. Любое изменение каталога или статуса квартиры
// вызывает Invalidate(), и все записи сбрасываются разом.
public class CatalogCache(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private CancellationTokenSource _reset = new();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
    {
        var cacheKey = "catalog:" + key;
        if (cache.TryGetValue(cacheKey, out T? cached))
            return cached!;

        var resetToken = _reset.Token;
        var value = await factory();

        // Если каталог изменился, пока мы читали базу, данные могли устареть: не кладём их в кэш.
        if (!resetToken.IsCancellationRequested)
        {
            cache.Set(cacheKey, value, new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(Lifetime)
                .AddExpirationToken(new CancellationChangeToken(resetToken)));
        }

        return value;
    }

    public void Invalidate()
    {
        var previous = Interlocked.Exchange(ref _reset, new CancellationTokenSource());
        previous.Cancel();
        previous.Dispose();
    }
}
