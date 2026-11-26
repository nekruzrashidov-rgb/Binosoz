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

    /// <summary>Эко-паспорт дома (контент-менеджер и руководители): заменяется целиком, пустые поля — «не заполнено».
    /// Изменение пишется в журнал, кэш каталога сбрасывается. В GET дома — поле eco, только заполненное.</summary>
    [HttpPut("{id:guid}/eco")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    public Task<BuildingResponse> UpdateEco(Guid id, SaveBuildingEcoRequest request, CancellationToken ct) =>
        catalog.UpdateBuildingEcoAsync(id, request, ct);

    /// <summary>Удобства дома (контент-менеджер и руководители): список ключей целиком, неизвестный ключ — 400.
    /// Изменение пишется в журнал, кэш каталога сбрасывается. В GET дома — поле amenities, только непустое.</summary>
    [HttpPut("{id:guid}/amenities")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    public Task<BuildingResponse> UpdateAmenities(Guid id, SaveBuildingAmenitiesRequest request, CancellationToken ct) =>
        catalog.UpdateBuildingAmenitiesAsync(id, request, ct);

    /// <summary>Точка дома на карте (широта, долгота — парой) и высота этажа в метрах (контент-менеджер и руководители).
    /// Без координат сайт не показывает окружение и расстояния; без высоты этажа считает 3,0 м с пометкой «оценка».</summary>
    [HttpPut("{id:guid}/location")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    public Task<BuildingResponse> UpdateLocation(Guid id, SaveBuildingLocationRequest request, CancellationToken ct) =>
        catalog.UpdateBuildingLocationAsync(id, request, ct);

    /// <summary>Блок ЖК (контент-менеджер и руководители): буква, порядок, контур в плане, место на участке, уступы —
    /// целиком. Неверный контур (меньше 3 или больше 64 вершин, самопересечение, нулевая площадь), уступ вне контура,
    /// пересечение с другим блоком или разрыв меньше Catalog:MinBlockGapMeters (20 м) — 400; буква занята — 409.
    /// Изменение пишется в журнал, кэш каталога сбрасывается.</summary>
    [HttpPut("{id:guid}/shape")]
    [Authorize(Roles = Roles.CatalogEditors)]
    [ProducesResponseType<BuildingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BuildingResponse> UpdateShape(Guid id, SaveBuildingShapeRequest request, CancellationToken ct) =>
        catalog.UpdateBuildingShapeAsync(id, request, ct);
}

// План общего участка ЖК своей организации (руководители): контур и зоны двора. На сайте — в GET /public/brand (sitePlan).
[ApiController]
[Route("api/v1/owner/site-plan")]
[Authorize(Roles = Roles.Leaders)]
[Produces("application/json")]
public class SitePlanController(ICatalogService catalog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<Sitora.Domain.Entities.SitePlan>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<Sitora.Domain.Entities.SitePlan>> Get(CancellationToken ct) =>
        await catalog.GetSitePlanAsync(ct) is { } plan ? plan : NoContent();

    [HttpPut]
    [ProducesResponseType<Sitora.Domain.Entities.SitePlan>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Sitora.Domain.Entities.SitePlan>> Put(SaveSitePlanRequest request, CancellationToken ct) =>
        await catalog.UpdateSitePlanAsync(request, ct) is { } plan ? plan : NoContent();
}
