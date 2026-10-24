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
