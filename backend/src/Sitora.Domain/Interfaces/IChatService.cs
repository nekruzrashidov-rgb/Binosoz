using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

public interface IChatService
{
    Task<bool> CanAccessAsync(Guid dealId, CancellationToken ct = default);

    // Сообщения от новых к старым; сообщения другой стороны отмечаются прочитанными.
    Task<PagedResult<ChatMessageResponse>> GetMessagesAsync(Guid dealId, int page, int pageSize, CancellationToken ct = default);

    Task<ChatMessageResponse> SendAsync(Guid dealId, string text, CancellationToken ct = default);

    // Сколько сообщений другой стороны не прочитано; сами сообщения не отмечаются.
    Task<ChatUnreadResponse> GetUnreadAsync(Guid dealId, CancellationToken ct = default);

    // Для сотрудника: сделки с перепиской, свежие сверху; поиск по имени или телефону покупателя.
    Task<IReadOnlyList<ChatConversationResponse>> GetConversationsAsync(string? search, CancellationToken ct = default);
}

// Доставка нового сообщения участникам в реальном времени (SignalR живёт в WebApi).
public interface IChatNotifier
{
    Task MessageCreatedAsync(ChatMessageResponse message, CancellationToken ct = default);
}

// Когда сотрудник последний раз обращался к API — для отметки «в сети» в чате покупателя.
// Хранится в общем кэше (Redis) 6 минут: после этого сотрудник считается «не в сети» и время не показывается.
public interface IPresenceTracker
{
    Task TouchAsync(Guid userId, CancellationToken ct = default);
    Task<DateTime?> LastSeenAsync(Guid userId, CancellationToken ct = default);
}
