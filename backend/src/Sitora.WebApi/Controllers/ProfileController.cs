using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Настройки своего профиля: любой вошедший меняет только себя.
[ApiController]
[Route("api/v1/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController(INotificationService notifications) : ControllerBase
{
    /// <summary>
    /// Привязать личный чат Telegram для уведомлений (платежи, просрочки, напоминания, брони, отчёты).
    /// chatId можно узнать у бота @userinfobot и предварительно написать /start нашему боту; null — отвязать.
    /// </summary>
    [HttpPost("telegram")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Telegram(TelegramChatRequest request, CancellationToken ct)
    {
        await notifications.SetTelegramChatAsync(request.ChatId, ct);
        return NoContent();
    }
}
