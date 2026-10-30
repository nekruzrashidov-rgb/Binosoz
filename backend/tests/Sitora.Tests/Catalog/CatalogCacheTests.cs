using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Кэш каталога поверх IDistributedCache: в тестах — кэш в памяти (Redis не нужен).
public class CatalogCacheTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private record Item(int Value, string Name);

    private static CatalogCache NewCache(IDistributedCache? store = null) =>
        new(store ?? new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())), NullLogger<CatalogCache>.Instance);

    [Fact]
    public async Task SecondCall_IsHit_AfterInvalidate_FactoryRunsAgain()
    {
        var cache = NewCache();
        var calls = 0;
        Task<Item> Factory() => Task.FromResult(new Item(++calls, "дом"));

        Assert.Equal(new Item(1, "дом"), await cache.GetOrCreateAsync("buildings", Factory));
        Assert.Equal(new Item(1, "дом"), await cache.GetOrCreateAsync("buildings", Factory));
        Assert.Equal(1, calls);

        await cache.InvalidateAsync();
        Assert.Equal(new Item(2, "дом"), await cache.GetOrCreateAsync("buildings", Factory));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task ChangeDuringRead_IsNotCached()
    {
        var cache = NewCache();
        var calls = 0;

        // Каталог изменили, пока читали базу: прочитанное могло устареть и в кэш не попадает.
        await cache.GetOrCreateAsync("apartments", async () => { calls++; await cache.InvalidateAsync(); return 1; });
        await cache.GetOrCreateAsync("apartments", () => Task.FromResult(++calls));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task BrokenStore_FallsBackToDatabase()
    {
        var cache = NewCache(new BrokenCache());

        Assert.Equal(7, await cache.GetOrCreateAsync("floors", () => Task.FromResult(7)));
        await cache.InvalidateAsync();
    }

    [Fact]
    public void WithoutRedis_TheApiUsesInMemoryDistributedCache()
    {
        Assert.IsType<MemoryDistributedCache>(factory.Services.GetRequiredService<IDistributedCache>());
    }

    // Как Redis, к которому нет подключения.
    private sealed class BrokenCache : IDistributedCache
    {
        private static Exception Down() => new InvalidOperationException("Redis недоступен");
        public byte[]? Get(string key) => throw Down();
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => throw Down();
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Down();
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => throw Down();
        public void Refresh(string key) => throw Down();
        public Task RefreshAsync(string key, CancellationToken token = default) => throw Down();
        public void Remove(string key) => throw Down();
        public Task RemoveAsync(string key, CancellationToken token = default) => throw Down();
    }
}
