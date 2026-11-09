using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// CRM: импорт клиентов, шаблоны сообщений, аналитика продаж (продажи и руководители), карточка проекта (руководители).
[ApiController]
[Route("api/v1/crm")]
[Authorize(Roles = Roles.Sales)]
[Produces("application/json")]
public class CrmToolsController(ICrmToolsService tools) : ControllerBase
{
    private const long MaxImportFileSize = 5 * 1024 * 1024;

    /// <summary>
    /// Импорт клиентов из .xlsx: столбцы Имя | Телефон | Email | Комментарий, первая строка — заголовки.
    /// dryRun=true — предпросмотр без записи. Повторы телефона пропускаются. Комментарий в карточку клиента не сохраняется.
    /// </summary>
    [HttpPost("clients/import")]
    [RequestSizeLimit(MaxImportFileSize)]
    [ProducesResponseType<ClientImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ClientImportResult> ImportClients(IFormFile file, [FromQuery] bool dryRun, CancellationToken ct)
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Нужен файл Excel в формате .xlsx");
        await using var stream = file.OpenReadStream();
        return await tools.ImportClientsAsync(stream, dryRun, ct);
    }

    /// <summary>Шаблоны сообщений организации (SMS / WhatsApp) с подстановками {name}, {apt}, {amount}.</summary>
    [HttpGet("templates")]
    public Task<IReadOnlyList<MessageTemplateResponse>> Templates(CancellationToken ct) => tools.TemplatesAsync(ct);

    /// <summary>Новый шаблон. Тип: payment, booking или welcome; название уникально в организации.</summary>
    [HttpPost("templates")]
    [ProducesResponseType<MessageTemplateResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MessageTemplateResponse>> CreateTemplate(MessageTemplateRequest request, CancellationToken ct)
    {
        var created = await tools.CreateTemplateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("templates/{id:guid}")]
    public Task<MessageTemplateResponse> UpdateTemplate(Guid id, MessageTemplateRequest request, CancellationToken ct) =>
        tools.UpdateTemplateAsync(id, request, ct);

    [HttpDelete("templates/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        await tools.DeleteTemplateAsync(id, ct);
        return NoContent();
    }

    /// <summary>Сделки по месяцам за 12 месяцев и воронка: заявки → брони → сделки → завершены.</summary>
    [HttpGet("analytics/sales")]
    public Task<SalesAnalyticsResponse> SalesAnalytics(CancellationToken ct) => tools.SalesAnalyticsAsync(ct);

    /// <summary>Карточка проекта по каждому дому руководителя: стройка, продажи, выручка и план в сомони.</summary>
    [HttpGet("analytics/project")]
    [Authorize(Roles = Roles.Leaders)]
    public Task<IReadOnlyList<ProjectCardResponse>> Project(CancellationToken ct) => tools.ProjectCardsAsync(ct);
}
