using System.Collections.Concurrent;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Services;

// Время последнего запроса сотрудника — в памяти процесса (одиночный экземпляр API).
public class PresenceTracker(TimeProvider timeProvider) : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, DateTime> _seen = new();

    public void Touch(Guid userId) => _seen[userId] = timeProvider.GetUtcNow().UtcDateTime;

    public DateTime? LastSeen(Guid userId) => _seen.TryGetValue(userId, out var at) ? at : null;
}
