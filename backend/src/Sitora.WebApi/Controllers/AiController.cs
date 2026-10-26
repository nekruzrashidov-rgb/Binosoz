using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// AI-помощник для вошедших: покупателю — вопросы о квартире и сделке, сотрудникам — документы, CRM и письма.
[ApiController]
[Route("api/v1/ai")]
[Authorize]
[Produces("application/json")]
public class AiController(IAiAssistant assistant) : ControllerBase
{
    /// <summary>
    /// Сообщение помощнику. history — предыдущие реплики { role: user|assistant, content } (хранятся только в браузере).
    /// 503 — помощник не настроен (нет ключа Claude:ApiKey) или временно недоступен.
    /// </summary>
    [HttpPost("chat")]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [ProducesResponseType<AiChatResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<AiChatResponse> Chat(AiChatRequest request, CancellationToken ct) =>
        assistant.ChatAsync(request, ct);
}
