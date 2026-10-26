using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/crm")]
[Authorize(Roles = Roles.Sales)]
[Produces("application/json")]
public class CrmController(ICrmService crm) : ControllerBase
{
    [HttpGet("summary")]
    public Task<CrmSummaryResponse> Summary(CancellationToken ct) => crm.GetSummaryAsync(ct);

    /// <summary>Колонки по этапам сделки; в «Заявке» и «Брони» также заявки и брони без сделки.</summary>
    [HttpGet("pipeline")]
    public Task<PipelineResponse> Pipeline(CancellationToken ct) => crm.GetPipelineAsync(ct);
}
