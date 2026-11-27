using Amazon.S3;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Health;

public static class HealthTags
{
    // Проверки зависимостей для /health/ready; /health/live их не запускает.
    public const string Ready = "ready";
}

// Хранилище документов и фото работает по-настоящему: пробный объект записывается, читается обратно, сверяется и удаляется.
// В Production файлы на диске без явного Storage__RootPath (папка внутри контейнера, на Render — временный диск)
// пропадут при перезапуске: тогда Degraded с объяснением, хотя запись и проходит.
// Причина отказа в ответ /health/ready не попадает, но пишется в лог (шаг, тип, код и HTTP-статус S3, без ключей).
public class StorageHealthCheck(IFileStorage storage, IOptions<StorageOptions> options, IHostEnvironment environment,
    ILogger<StorageHealthCheck> logger, TimeProvider timeProvider) : IHealthCheck
{
    // /health/ready опрашивают часто: одинаковый отказ пишется в лог не чаще раза в минуту.
    private static readonly TimeSpan LogInterval = TimeSpan.FromMinutes(1);
    private static readonly Lock LogGate = new();
    private static string? _lastLogged;
    private static DateTimeOffset _lastLoggedAt;

    public const string EphemeralWarning =
        "Файлы хранятся локально на временном (эфемерном) диске контейнера и пропадут при перезапуске: подключите S3/R2 (Storage__Provider=S3 и Storage__S3__*) или задайте Storage__RootPath на постоянном томе";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var key = $"health/probe-{Guid.NewGuid():N}.tmp";
        var probe = System.Security.Cryptography.RandomNumberGenerator.GetBytes(16);
        var step = "запись";
        try
        {
            await storage.SaveAsync(key, new MemoryStream(probe), ct);
            try
            {
                step = "чтение";
                await using var read = await storage.OpenReadAsync(key, ct);
                using var copy = new MemoryStream();
                await read.CopyToAsync(copy, ct);
                if (!copy.ToArray().AsSpan().SequenceEqual(probe))
                    return HealthCheckResult.Unhealthy("Хранилище файлов вернуло не то, что было записано");
            }
            finally
            {
                step = "удаление";
                await storage.DeleteAsync(key, ct);
            }
        }
        catch (Exception e)
        {
            LogFailure(step, e);
            return HealthCheckResult.Unhealthy("Хранилище файлов недоступно", e);
        }

        return IsEphemeral(options.Value, environment)
            ? HealthCheckResult.Degraded(EphemeralWarning)
            : HealthCheckResult.Healthy();
    }

    private void LogFailure(string step, Exception e)
    {
        var s3 = e as AmazonS3Exception;
        var signature = $"{step}|{e.GetType().FullName}|{s3?.ErrorCode}|{(int?)s3?.StatusCode}";
        var now = timeProvider.GetUtcNow();
        lock (LogGate)
        {
            if (signature == _lastLogged && now - _lastLoggedAt < LogInterval)
                return;
            _lastLogged = signature;
            _lastLoggedAt = now;
        }

        if (s3 is not null)
            logger.LogError(e, "Проверка хранилища: {Step} не удалась — {ExceptionType} {ErrorCode} HTTP {StatusCode} ({ErrorType}): {Message}",
                step, e.GetType().Name, s3.ErrorCode, (int)s3.StatusCode, s3.ErrorType, e.Message);
        else
            logger.LogError(e, "Проверка хранилища: {Step} не удалась — {ExceptionType}: {Message}", step, e.GetType().Name, e.Message);
    }

    public static bool IsEphemeral(StorageOptions o, IHostEnvironment environment) =>
        environment.IsProduction() && !o.IsS3 && string.IsNullOrWhiteSpace(o.RootPath);
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
