using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class DocumentsController(IDocumentService documents) : ControllerBase
{
    [HttpGet("deals/{dealId:guid}/documents")]
    [Authorize(Roles = Roles.DealViewers)]
    [ProducesResponseType<IReadOnlyList<DocumentResponse>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<DocumentResponse>> List(Guid dealId, CancellationToken ct) =>
        documents.GetAsync(dealId, ct);

    /// <summary>Загрузка документа по сделке: PDF, PNG, JPG или DOCX до 20 МБ.</summary>
    [HttpPost("deals/{dealId:guid}/documents")]
    [Authorize(Roles = Roles.DealStaff)]
    [RequestSizeLimit(DocumentService.MaxFileSize + 64 * 1024)]
    [ProducesResponseType<DocumentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DocumentResponse>> Upload(
        Guid dealId, [FromForm] DocumentType type, [FromForm] DateTime? signedAt, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var document = await documents.UploadAsync(dealId,
            new UploadDocumentRequest(type, signedAt, file.FileName, file.ContentType, file.Length, stream), ct);
        return StatusCode(StatusCodes.Status201Created, document);
    }

    /// <summary>Подписанная ссылка на скачивание документа, действует 15 минут.</summary>
    [HttpGet("documents/{id:guid}/download")]
    [Authorize(Roles = Roles.DealViewers)]
    [ProducesResponseType<FileLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<FileLinkResponse> Download(Guid id, CancellationToken ct) =>
        documents.CreateDownloadLinkAsync(id, ct);
}
