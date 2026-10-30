using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Services;

// Время последнего запроса сотрудника: ключ presence:{userId} в общем кэше (Redis) на 6 минут —
// 5 минут окна «в сети» и минута запаса. Чтобы не писать в Redis на каждый запрос, экземпляр API обновляет
// отметку не чаще раза в минуту. Сбой кэша не мешает запросу: сотрудник просто будет «не в сети».
public class PresenceTracker(IDistributedCache cache, TimeProvider timeProvider, ILogger<PresenceTracker> logger) : IPresenceTracker
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan WriteEvery = TimeSpan.FromMinutes(1);
    private readonly ConcurrentDictionary<Guid, DateTime> _written = new();

    public async Task TouchAsync(Guid userId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (_written.TryGetValue(userId, out var last) && now - last < WriteEvery)
            return;
        _written[userId] = now;
        try
        {
            await cache.SetStringAsync(Key(userId), now.ToString("O", CultureInfo.InvariantCulture),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime }, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _written.TryRemove(userId, out _);
            logger.LogWarning(e, "Не удалось отметить сотрудника {UserId} «в сети»", userId);
        }
    }

    public async Task<DateTime?> LastSeenAsync(Guid userId, CancellationToken ct = default)
    {
        try
        {
            var value = await cache.GetStringAsync(Key(userId), ct);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "Не удалось узнать, в сети ли сотрудник {UserId}", userId);
            return null;
        }
    }

    private static string Key(Guid userId) => $"presence:{userId:N}";
}
