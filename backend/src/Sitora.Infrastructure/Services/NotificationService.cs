using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class NotificationService(AppDbContext db, ICurrentUser currentUser, TimeProvider timeProvider) : INotificationService
{
    public const int Limit = 50;

    public async Task<NotificationListResponse> GetMineAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        var mine = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var items = await mine.OrderByDescending(n => n.CreatedAt).Take(Limit)
            .Select(n => new NotificationResponse(n.Id, n.Title, n.Text, n.Kind, n.DealId, n.ReadAt != null, n.CreatedAt))
            .ToListAsync(ct);
        return new NotificationListResponse(await mine.CountAsync(n => n.ReadAt == null, ct), items);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken ct = default)
    {
        var userId = UserId;
        var notification = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct)
            ?? throw new NotFoundException("Уведомление не найдено");
        notification.ReadAt ??= timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = UserId;
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
}
