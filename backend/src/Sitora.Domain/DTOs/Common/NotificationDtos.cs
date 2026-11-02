namespace Sitora.Domain.DTOs.Common;

// kind: overdue_payment, payment_reminder или null (сообщения о платежах, документах, чате).
public record NotificationResponse(Guid Id, string? Title, string Text, string? Kind, Guid? DealId, bool IsRead, DateTime CreatedAt);

public record NotificationListResponse(int UnreadCount, IReadOnlyList<NotificationResponse> Items);

// chatId — из бота @userinfobot; null — отвязать Telegram.
public record TelegramChatRequest(long? ChatId);
