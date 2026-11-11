using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/buildings")]
[Produces("application/json")]
public class BuildingsController(ICatalogService catalog) : ControllerBase
{
    /// <summary>
    /// Дома застройщика; organization — его короткое имя (slug). Гость без organization — 400 «Укажите организацию»,
    /// неизвестная или отключённая организация — 404. Сотрудник со входом без organization видит дома своей организации.
    /// </summary>
    [HttpGet]
    [OutputCache(PolicyName = CachingSetup.Catalog)]
    [AllowAnonymous]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<PagedResult<BuildingResponse>> List(int page = 1, int pageSize = PageRequest.DefaultPageSize,
        string? organization = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(organization))
        {
            if (User.Identity?.IsAuthenticated != true)
                throw new BadRequestException("Укажите организацию");
        }
        else if (await catalog.FindOrganizationIdAsync(organization, ct) is null)
            throw new NotFoundException("Организация не найдена");

        return await catalog.GetBuildingsAsync(page, pageSize, organization, ct);
    }

    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = CachingSetup.Catalog)]
    [AllowAnonymous]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<BuildingResponse> Get(Guid id, CancellationToken ct) =>
        catalog.GetBuildingAsync(id, ct);

    [HttpGet("{id:guid}/floors")]
    [OutputCache(PolicyName = CachingSetup.Catalog)]
    [AllowAnonymous]
    [ProducesResponseType<PagedResult<FloorResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PagedResult<FloorResponse>> Floors(Guid id, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        catalog.GetFloorsAsync(id, page, pageSize, ct);

    /// <summary>Новый дом в организации владельца; админ объекта и контент-менеджер дома не создают.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.OrganizationLeaders)]
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
