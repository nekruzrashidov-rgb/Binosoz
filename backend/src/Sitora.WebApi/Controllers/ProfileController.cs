using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Services;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Настройки своего профиля: любой вошедший меняет только себя.
[ApiController]
[Route("api/v1/profile")]
[Authorize]
[Produces("application/json")]
public class ProfileController(INotificationService notifications, TelegramLinkService telegramLinks) : ControllerBase
{
    /// <summary>
    /// Привязать личный чат Telegram вручную по chat id (для сотрудников; покупателю удобнее POST /profile/telegram/link).
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

    /// <summary>
    /// «Получать уведомления в Telegram»: ссылка https://t.me/&lt;бот&gt;?start=&lt;токен&gt; — одноразовая, 15 минут.
    /// Покупатель открывает её и нажимает «Старт»: бот сохраняет чат. Бот не настроен — 503 telegram_not_configured.
    /// </summary>
    [HttpPost("telegram/link")]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<TelegramLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<TelegramLinkResponse> TelegramLink(CancellationToken ct) =>
        telegramLinks.CreateLinkAsync(User.GetUserId(), BuyerTexts.FromHeader(Request.Headers.AcceptLanguage), ct);

    /// <summary>Отвязать Telegram: сообщения в бот больше не приходят (то же делает команда /stop в боте).</summary>
    [HttpDelete("telegram")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnlinkTelegram(CancellationToken ct)
    {
        await telegramLinks.UnlinkAsync(User.GetUserId(), ct);
        return NoContent();
    }

    /// <summary>Настройки уведомлений: каналы (Telegram, почта), события (бронь, сделка, платежи, документы, чат), что подключено.</summary>
    [HttpGet("notifications")]
    [ProducesResponseType<NotificationSettingsResponse>(StatusCodes.Status200OK)]
    public Task<NotificationSettingsResponse> NotificationSettings(CancellationToken ct) => notifications.GetSettingsAsync(ct);

    /// <summary>Сохранить настройки уведомлений; язык сообщений — язык сайта (Accept-Language: ru, tg, en).</summary>
    [HttpPut("notifications")]
    [ProducesResponseType<NotificationSettingsResponse>(StatusCodes.Status200OK)]
    public Task<NotificationSettingsResponse> SaveNotificationSettings(UpdateNotificationSettingsRequest request, CancellationToken ct) =>
        notifications.SaveSettingsAsync(request, BuyerTexts.FromHeader(Request.Headers.AcceptLanguage), ct);
}
