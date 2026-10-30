namespace Sitora.Domain.DTOs.Chat;

public record SendMessageRequest(string Text);

public record ChatMessageResponse(
    Guid Id,
    Guid DealId,
    Guid SenderId,
    string SenderName,
    bool FromCustomer,
    string Text,
    DateTime CreatedAt,
    DateTime? ReadAt);

// Переписка в списке сотрудника: последнее сообщение и сколько сообщений покупателя ещё не прочитано.
public record ChatConversationResponse(
    Guid DealId,
    string CustomerName,
    string CustomerPhone,
    int ApartmentNumber,
    string BuildingName,
    string LastMessage,
    DateTime LastMessageAt,
    bool LastFromCustomer,
    int Unread);

// Непрочитанное для читающей стороны (без отметки о прочтении) и менеджер сделки: в сети, если заходил за последние 5 минут.
public record ChatUnreadResponse(int Unread, string? ManagerName, bool ManagerOnline, DateTime? ManagerLastSeenAt);
