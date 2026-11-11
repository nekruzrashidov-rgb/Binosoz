using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/floors")]
[Produces("application/json")]
public class FloorsController(ICatalogService catalog) : ControllerBase
{
    [HttpGet("{id:guid}/apartments")]
    [OutputCache(PolicyName = CachingSetup.Catalog)]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<ApartmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResult<ApartmentResponse>> Apartments(Guid id, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        catalog.GetApartmentsAsync(id, page, pageSize, ct);

    [HttpPost]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<FloorResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FloorResponse>> Create(CreateFloorRequest request, CancellationToken ct)
    {
        var floor = await catalog.CreateFloorAsync(request, ct);
        return Created($"/api/v1/floors/{floor.Id}/apartments", floor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<FloorResponse>(StatusCodes.Status200OK)]
    public Task<FloorResponse> Update(Guid id, UpdateFloorRequest request, CancellationToken ct) =>
        catalog.UpdateFloorAsync(id, request, ct);
}
