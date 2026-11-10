using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/deals")]
[Produces("application/json")]
// Права на методах: список и карточку читает и бухгалтер (ему нужно найти сделку, чтобы внести платёж),
// создают сделки и меняют этап только продажи. Атрибуты на классе и методе складывались бы через «и».
public class DealsController(IDealService deals) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.DealStaff)]
    public Task<PagedResult<DealResponse>> List(DealStage? stage, Guid? customerId, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        deals.GetAsync(stage, customerId, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.DealStaff)]
    [ProducesResponseType<DealResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<DealResponse> Get(Guid id, CancellationToken ct) =>
        deals.GetByIdAsync(id, ct);

    [HttpPost]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<DealResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealResponse>> Create(CreateDealRequest request, CancellationToken ct)
    {
        var deal = await deals.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = deal.Id }, deal);
    }

    /// <summary>Вперёд — свободно; назад и отмена — только с причиной.</summary>
    [HttpPatch("{id:guid}/stage")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<DealResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<DealResponse> ChangeStage(Guid id, ChangeDealStageRequest request, CancellationToken ct) =>
        deals.ChangeStageAsync(id, request, ct);
}
