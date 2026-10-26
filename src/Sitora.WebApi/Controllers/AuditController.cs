using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/audit")]
[Authorize(Roles = Roles.OrganizationLeaders)]
[Produces("application/json")]
public class AuditController(IAuditService audit) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogResponse>> List([FromQuery] AuditQuery query, CancellationToken ct) =>
        audit.GetAsync(query, ct);
}
