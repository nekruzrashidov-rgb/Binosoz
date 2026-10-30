using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace Sitora.Infrastructure.Integrations;

// Общий кэш (раздел Redis; в .env — Redis__ConnectionString). Пустая строка подключения — кэш в памяти процесса:
// так работают разработка без Redis и тесты. С несколькими экземплярами API Redis обязателен.
public class RedisOptions
{
    public const string SectionName = "Redis";

    public string ConnectionString { get; set; } = "";

    // Приставка ко всем ключам: разные окружения могут делить один Redis.
    public string InstanceName { get; set; } = "binosoz:";

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);

    public static IDistributedCache CreateCache(RedisOptions options) => options.Enabled
        ? new RedisCache(Options.Create(new RedisCacheOptions { Configuration = options.ConnectionString, InstanceName = options.InstanceName }))
        : new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
}
