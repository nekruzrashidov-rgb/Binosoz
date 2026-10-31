using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Проверка документов и чтение чеков с помощью ИИ (Claude). Каждый запрос — платный вызов модели,
// поэтому тот же лимит, что у AI-помощника. 503 — ИИ не настроен (нет Claude:ApiKey) или временно недоступен.
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicies.Ai)]
public class AiDocumentsController(IAiDocumentService documents) : ControllerBase
{
    /// <summary>Проверить уже загруженный документ сделки: ФИО (по умолчанию — покупателя сделки) и срок действия.</summary>
    [HttpPost("documents/{id:guid}/verify")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<DocumentVerifyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<DocumentVerifyResult> VerifyStored(Guid id, string? expectedFullName, CancellationToken ct) =>
        documents.VerifyStoredAsync(id, expectedFullName, ct);

    /// <summary>Проверить новый файл (изображение до 5 МБ или PDF до 10 МБ), не сохраняя его.</summary>
    [HttpPost("deals/{dealId:guid}/documents/verify")]
    [Authorize(Roles = Roles.Sales)]
    [RequestSizeLimit(ClaudeDocumentVerifier.MaxPdfSize + 64 * 1024)]
    [ProducesResponseType<DocumentVerifyResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<DocumentVerifyResult> VerifyUpload(Guid dealId, IFormFile file, [FromForm] string? expectedFullName, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await documents.VerifyUploadAsync(dealId, new AiFile(file.FileName, file.ContentType, file.Length, stream), expectedFullName, ct);
    }

    /// <summary>Прочитать банковский чек и подобрать неоплаченный пункт графика (сумма ±1 %, срок ±30 дней). Платёж не создаётся.</summary>
    [HttpPost("deals/{dealId:guid}/payments/read-receipt")]
    [Authorize(Roles = Roles.DealStaff)]
    [RequestSizeLimit(ClaudeDocumentVerifier.MaxPdfSize + 64 * 1024)]
    [ProducesResponseType<ReceiptReadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ReceiptReadResponse> ReadReceipt(Guid dealId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await documents.ReadReceiptAsync(dealId, new AiFile(file.FileName, file.ContentType, file.Length, stream), ct);
    }
}
