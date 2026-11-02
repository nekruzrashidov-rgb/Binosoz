using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Интерактивные планы этажей для CRM: смотрят сотрудники продаж, бухгалтерия и контент, меняют руководители.
// Чужой дом — 404 (фильтры домов в AppDbContext).
[ApiController]
[Route("api/v1/buildings/{buildingId:guid}/floorplan")]
[Authorize(Roles = Roles.DealStaff + "," + Roles.CatalogEditors)]
public class FloorPlansController(IFloorPlanService plans) : ControllerBase
{
    /// <summary>Номера этажей дома, для которых загружен план.</summary>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyList<int>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<int>> Floors(Guid buildingId, CancellationToken ct) => plans.FloorsWithPlanAsync(buildingId, ct);

    /// <summary>
    /// SVG этажа: у прямоугольника квартиры цвет по статусу (свободна #22c55e, бронь #f59e0b, продана #ef4444, по запросу #6b7280),
    /// подсказка &lt;title&gt; «Кв. N, площадь м², цена валюта» и data-атрибуты: apartment-id, number, status, deal-id (у брони и продажи).
    /// </summary>
    [HttpGet("{floor:int}")]
    [Produces("image/svg+xml")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid buildingId, int floor, CancellationToken ct) =>
        Content(await plans.RenderAsync(buildingId, floor, ct), "image/svg+xml; charset=utf-8");

    /// <summary>
    /// Сохранить шаблон этажа. Разрешены svg, g, rect, text, tspan, title, desc, line, path, polygon, polyline, circle, ellipse;
    /// обработчики on*, ссылки href и url() в стилях убираются. Аудит FloorPlan / Updated.
    /// </summary>
    [HttpPut("{floor:int}")]
    [Authorize(Roles = Roles.Leaders)]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Save(Guid buildingId, int floor, SaveFloorPlanRequest request, CancellationToken ct)
    {
        await plans.SaveAsync(buildingId, floor, request.SvgTemplate, ct);
        return NoContent();
    }
}
