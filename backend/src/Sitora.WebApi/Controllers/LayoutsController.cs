using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Controllers;

// Планировки застройщика (CRM → «Планировки»): JSON в формате сайта. Квартира с LayoutCode = code рисуется по ней.
[ApiController]
[Route("api/v1/layouts")]
[Authorize(Roles = Roles.CatalogEditors)]
[Produces("application/json")]
public class LayoutsController(LayoutService layouts) : ControllerBase
{
    /// <summary>Планировки своей организации.</summary>
    [HttpGet]
    public Task<IReadOnlyList<LayoutResponse>> List(CancellationToken ct) => layouts.GetOwnAsync(ct);

    /// <summary>Сохранить планировку (новую или заменить): контур 3–64 вершины без самопересечений, 1–30 комнат-многоугольников
    /// внутри контура, сумма 10–1 000 м². Ошибка — 400; журнал Created/Updated.</summary>
    [HttpPut("{code}")]
    [ProducesResponseType<LayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<LayoutResponse> Save(string code, SaveLayoutRequest request, CancellationToken ct) => layouts.SaveAsync(code, request, ct);

    [HttpDelete("{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string code, CancellationToken ct)
    {
        await layouts.DeleteAsync(code, ct);
        return NoContent();
    }
}

// Планировки застройщика для сайта (без входа).
[ApiController]
[Route("api/v1/public/{organization}/layouts")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicLayoutsController(LayoutService layouts) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<LayoutResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<LayoutResponse>> List(string organization, CancellationToken ct) => layouts.GetPublicAsync(organization, ct);
}
