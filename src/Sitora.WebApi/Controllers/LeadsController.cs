using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/leads")]
[Produces("application/json")]
public class LeadsController(ILeadService leads) : ControllerBase
{
    /// <summary>Заявка с сайта; вход не нужен.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType<LeadResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadResponse>> Create(CreateLeadRequest request, CancellationToken ct)
    {
        var lead = await leads.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, lead);
    }

    [HttpGet]
    [Authorize(Roles = Roles.Sales)]
    public Task<PagedResult<LeadResponse>> List(LeadStatus? status, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        leads.GetAsync(status, page, pageSize, ct);

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<LeadResponse>(StatusCodes.Status200OK)]
    public Task<LeadResponse> Update(Guid id, UpdateLeadRequest request, CancellationToken ct) =>
        leads.UpdateAsync(id, request, ct);
}
