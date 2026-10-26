using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

public interface IChatService
{
    Task<bool> CanAccessAsync(Guid dealId, CancellationToken ct = default);

    // Сообщения от новых к старым; сообщения другой стороны отмечаются прочитанными.
    Task<PagedResult<ChatMessageResponse>> GetMessagesAsync(Guid dealId, int page, int pageSize, CancellationToken ct = default);

    Task<ChatMessageResponse> SendAsync(Guid dealId, string text, CancellationToken ct = default);
}

// Доставка нового сообщения участникам в реальном времени (SignalR живёт в WebApi).
public interface IChatNotifier
{
    Task MessageCreatedAsync(ChatMessageResponse message, CancellationToken ct = default);
}
