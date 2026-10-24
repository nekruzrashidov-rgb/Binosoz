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

    [HttpPost]
    [ProducesResponseType<ChatMessageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatMessageResponse>> Send(Guid dealId, SendMessageRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await chat.SendAsync(dealId, request.Text, ct));
}
