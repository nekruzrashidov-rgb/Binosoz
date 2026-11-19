namespace Sitora.Domain.DTOs.Common;

// kind: overdue_payment, payment_reminder или null (сообщения о платежах, документах, чате).
public record NotificationResponse(Guid Id, string? Title, string Text, string? Kind, Guid? DealId, bool IsRead, DateTime CreatedAt);

public record NotificationListResponse(int UnreadCount, IReadOnlyList<NotificationResponse> Items);

// chatId — из бота @userinfobot; null — отвязать Telegram.
public record TelegramChatRequest(long? ChatId);

// Настройки уведомлений покупателя: каналы (Telegram и почта), какие события присылать, что подключено.
// TelegramAvailable — бот настроен на сервере; EmailAvailable — почта подтверждена; Language — язык сообщений.
public record NotificationSettingsResponse(bool Telegram, bool Email, NotificationEventsDto Events,
    bool TelegramLinked, bool TelegramAvailable, bool EmailAvailable, string Language);

public record NotificationEventsDto(bool Bookings, bool Deal, bool Payments, bool Documents, bool Chat);

public record UpdateNotificationSettingsRequest(bool Telegram, bool Email, NotificationEventsDto Events);

// Ссылка на бота: https://t.me/<бот>?start=<одноразовый токен>; живёт 15 минут.
public record TelegramLinkResponse(string Url, DateTime ExpiresAt);

// Журнал напоминаний для CRM: кому, когда, по какому каналу и чем кончилось.
public record ReminderLogItem(Guid Id, string Event, string Channel, string Status, DateOnly? DueDate, DateTime CreatedAt,
    DateTime? SentAt, string? Error, string? CreatedBy, string Customer, int? Apartment);

public record RemindNowRequest(DateOnly DueDate);

public record RemindNowResponse(int Queued);
