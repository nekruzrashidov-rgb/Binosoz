using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Chat;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/deals/{dealId:guid}/messages")]
[Authorize(Roles = Roles.DealViewers)]
[Produces("application/json")]
public class ChatController(IChatService chat) : ControllerBase
{
    /// <summary>Сообщения от новых к старым; сообщения другой стороны отмечаются прочитанными.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ChatMessageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResult<ChatMessageResponse>> List(Guid dealId, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        chat.GetMessagesAsync(dealId, page, pageSize, ct);

    /// <summary>Сколько сообщений другой стороны не прочитано (без отметки о прочтении) и в сети ли менеджер сделки.</summary>
    [HttpGet("unread")]
    [ProducesResponseType<ChatUnreadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ChatUnreadResponse> Unread(Guid dealId, CancellationToken ct) =>
        chat.GetUnreadAsync(dealId, ct);

    [HttpPost]
    [ProducesResponseType<ChatMessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageResponse>> Send(Guid dealId, SendMessageRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await chat.SendAsync(dealId, request.Text, ct));
}

// Переписки по сделкам для сотрудников: свежие сверху, с числом непрочитанных сообщений покупателя.
[ApiController]
[Route("api/v1/chats")]
[Authorize(Roles = Roles.DealStaff)]
[Produces("application/json")]
public class ChatsController(IChatService chat) : ControllerBase
{
    /// <summary>Сделки с перепиской (до 200), от последнего сообщения к старым; search — имя или телефон покупателя.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ChatConversationResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<ChatConversationResponse>> List(string? search, CancellationToken ct) =>
        chat.GetConversationsAsync(search, ct);
}
