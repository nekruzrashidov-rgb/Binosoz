using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ConstructionController(IConstructionService construction) : ControllerBase
{
    private const string ProgressViewers = Roles.DealViewers + "," + Sitora.Domain.Enums.RoleNames.Content;

    /// <summary>Этапы стройки с процентами и фото. Покупатель видит их, только если у него есть сделка в этом доме.</summary>
    [HttpGet("buildings/{buildingId:guid}/stages")]
    [Authorize(Roles = ProgressViewers)]
    [ProducesResponseType<BuildingProgressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BuildingProgressResponse> Stages(Guid buildingId, CancellationToken ct) =>
        construction.GetStagesAsync(buildingId, ct);

    [HttpPost("buildings/{buildingId:guid}/stages")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<StageResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<StageResponse>> CreateStage(Guid buildingId, SaveStageRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await construction.CreateStageAsync(buildingId, request, ct));

    [HttpPut("stages/{stageId:guid}")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<StageResponse>(StatusCodes.Status200OK)]
    public Task<StageResponse> UpdateStage(Guid stageId, SaveStageRequest request, CancellationToken ct) =>
        construction.UpdateStageAsync(stageId, request, ct);

    /// <summary>Фото стройки: PNG или JPG до 10 МБ.</summary>
    [HttpPost("stages/{stageId:guid}/photos")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [RequestSizeLimit(ConstructionService.MaxPhotoSize + 64 * 1024)]
    [ProducesResponseType<PhotoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PhotoResponse>> AddPhoto(
        Guid stageId, IFormFile file, [FromForm] string? caption, [FromForm] DateTime? takenAt, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var photo = await construction.AddPhotoAsync(stageId, new UploadPhotoRequest(file.FileName, file.Length, stream, caption, takenAt), ct);
        return StatusCode(StatusCodes.Status201Created, photo);
    }

    [HttpGet("construction/photos/{photoId:guid}")]
    [Authorize(Roles = ProgressViewers)]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Photo(Guid photoId, CancellationToken ct)
    {
        var photo = await construction.GetPhotoAsync(photoId, ct);
        return File(photo.Content, photo.ContentType);
    }
}
