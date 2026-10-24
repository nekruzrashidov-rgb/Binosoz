using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/buildings")]
[Produces("application/json")]
public class BuildingsController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public Task<PagedResult<BuildingResponse>> List(int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        catalog.GetBuildingsAsync(page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BuildingResponse> Get(Guid id, CancellationToken ct) =>
        catalog.GetBuildingAsync(id, ct);

    [HttpGet("{id:guid}/floors")]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<FloorResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResult<FloorResponse>> Floors(Guid id, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        catalog.GetFloorsAsync(id, page, pageSize, ct);

    [HttpPost]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<BuildingResponse>> Create(SaveBuildingRequest request, CancellationToken ct)
    {
        var building = await catalog.CreateBuildingAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = building.Id }, building);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    public Task<BuildingResponse> Update(Guid id, SaveBuildingRequest request, CancellationToken ct) =>
        catalog.UpdateBuildingAsync(id, request, ct);
}
