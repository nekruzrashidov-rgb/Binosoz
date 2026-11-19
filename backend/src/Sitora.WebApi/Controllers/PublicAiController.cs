using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// AI-помощник для гостей публичного сайта застройщика: только вопросы о покупке квартиры в этом ЖК, вход не нужен.
[ApiController]
[Route("api/v1/public/{orgSlug}/ai")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicAiController(IPublicAiAssistant assistant) : ControllerBase
{
    /// <summary>Включён ли помощник: false — ключ Claude не задан, сайт не показывает пузырь. Нет организации — 404.</summary>
    [HttpGet("status")]
    [ProducesResponseType<AiStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AiStatusResponse> Status(string orgSlug, CancellationToken ct) => assistant.StatusAsync(orgSlug, ct);

    /// <summary>
    /// Вопрос гостя (≤ 500 символов). history — последние реплики { role: user|assistant, content } из браузера,
    /// учитываются 10 последних. 400 — пустой или длинный вопрос; 404 — нет организации или она приостановлена;
    /// 429 ai_public_busy — лимит по IP или суточный лимит организации (в ответе — contacts); 503 — помощник не настроен.
    /// </summary>
    [HttpPost("chat")]
    [EnableRateLimiting(RateLimitPolicies.AiPublic)]
    [ProducesResponseType<AiChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<AiChatResponse> Chat(string orgSlug, AiChatRequest request, CancellationToken ct) =>
        assistant.ChatAsync(orgSlug, request, ct);
}
