using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Sales;
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
}
