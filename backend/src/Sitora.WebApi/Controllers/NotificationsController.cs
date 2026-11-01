using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Колокольчик: свои уведомления у любого вошедшего — сотрудника, покупателя, создателя платформы.
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController(INotificationService notifications) : ControllerBase
{
    /// <summary>Последние 50 уведомлений, новые сверху, и число непрочитанных.</summary>
    [HttpGet]
    [ProducesResponseType<NotificationListResponse>(StatusCodes.Status200OK)]
    public Task<NotificationListResponse> Mine(CancellationToken ct) => notifications.GetMineAsync(ct);

    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Read(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ReadAll(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return NoContent();
    }
}
