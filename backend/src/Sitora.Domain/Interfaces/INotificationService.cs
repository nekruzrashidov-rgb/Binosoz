using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

// Колокольчик: уведомления текущего пользователя. Чужое уведомление — 404.
public interface INotificationService
{
    Task<NotificationListResponse> GetMineAsync(CancellationToken ct = default);
    Task MarkReadAsync(Guid id, CancellationToken ct = default);
    Task MarkAllReadAsync(CancellationToken ct = default);

    // Личный чат Telegram для уведомлений текущего пользователя; null — отвязать.
    Task SetTelegramChatAsync(long? chatId, CancellationToken ct = default);

    Task<NotificationSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    // language — язык сайта, на котором покупатель сохранил настройки (ru, tg, en): на нём придут сообщения.
    Task<NotificationSettingsResponse> SaveSettingsAsync(UpdateNotificationSettingsRequest request, string language, CancellationToken ct = default);
}
