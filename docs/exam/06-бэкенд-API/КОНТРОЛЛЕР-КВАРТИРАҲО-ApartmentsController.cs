using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/apartments")]
[Produces("application/json")]
public class ApartmentsController(ICatalogService catalog) : ControllerBase
{
    private const long MaxImportFileSize = 5 * 1024 * 1024;

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<ApartmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ApartmentResponse> Get(Guid id, CancellationToken ct) =>
        catalog.GetApartmentAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<ApartmentResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApartmentResponse>> Create(CreateApartmentRequest request, CancellationToken ct)
    {
        var apartment = await catalog.CreateApartmentAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = apartment.Id }, apartment);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<ApartmentResponse>(StatusCodes.Status200OK)]
    public Task<ApartmentResponse> Update(Guid id, UpdateApartmentRequest request, CancellationToken ct) =>
        catalog.UpdateApartmentAsync(id, request, ct);

    /// <summary>Перевести свободную квартиру в статус «под запрос» и обратно.</summary>
    [HttpPatch("{id:guid}/on-request")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<ApartmentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ApartmentResponse> SetOnRequest(Guid id, SetOnRequestRequest request, CancellationToken ct) =>
        catalog.SetOnRequestAsync(id, request.OnRequest, ct);

    /// <summary>Импорт квартир из .xlsx: столбцы этаж, номер, комнат, площадь, цена за м².</summary>
    [HttpPost("import")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [RequestSizeLimit(MaxImportFileSize)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ImportResult>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ImportResult>> Import([FromQuery] Guid buildingId, IFormFile file, CancellationToken ct)
    {
        if (!Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("Нужен файл Excel в формате .xlsx");

        await using var stream = file.OpenReadStream();
        var result = await catalog.ImportApartmentsAsync(buildingId, stream, ct);
        return result.Succeeded ? Ok(result) : UnprocessableEntity(result);
    }
}
