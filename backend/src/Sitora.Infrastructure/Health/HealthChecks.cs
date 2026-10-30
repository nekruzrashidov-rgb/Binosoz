using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Health;

public static class HealthTags
{
    // Проверки зависимостей для /health/ready; /health/live их не запускает.
    public const string Ready = "ready";
}

// Хранилище документов и фото доступно на запись: пробный файл создаётся и удаляется.
public class StorageHealthCheck(IFileStorage storage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var key = $"health/probe-{Guid.NewGuid():N}.tmp";
        try
        {
            await storage.SaveAsync(key, new MemoryStream([1]), ct);
            await storage.DeleteAsync(key, ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception e)
        {
            return HealthCheckResult.Unhealthy("Хранилище файлов недоступно на запись", e);
        }
    }
}

// Сервер фоновых задач (снятие броней, курс) жив: недавний heartbeat в хранилище Hangfire.
public class BackgroundJobsHealthCheck(JobStorage storage, TimeProvider timeProvider) : IHealthCheck
{
    private static readonly TimeSpan MaxHeartbeatAge = TimeSpan.FromMinutes(2);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var threshold = timeProvider.GetUtcNow().UtcDateTime - MaxHeartbeatAge;
        var alive = storage.GetMonitoringApi().Servers().Any(s => s.Heartbeat >= threshold);
        return Task.FromResult(alive
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "Сервер фоновых задач не отвечает"));
    }
}

// Общий кэш (Redis) отвечает: пробная запись и чтение. Без него API продолжает работать, каталог читается из базы,
// но вход создателя платформы и блокировки входа ломаются — поэтому Degraded, а не Unhealthy.
public class CacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var key = $"health:{Guid.NewGuid():N}";
        try
        {
            await cache.SetStringAsync(key, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) }, ct);
            var ok = await cache.GetStringAsync(key, ct) == "1";
            await cache.RemoveAsync(key, ct);
            return ok ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus, "Кэш не вернул записанное значение");
        }
        catch (Exception e)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Redis недоступен", e);
        }
    }
}
