using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

// Колокольчик: уведомления текущего пользователя. Чужое уведомление — 404.
public interface INotificationService
{
    Task<NotificationListResponse> GetMineAsync(CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);
}
