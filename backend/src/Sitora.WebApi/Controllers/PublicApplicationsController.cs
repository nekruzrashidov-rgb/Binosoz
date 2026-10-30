using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Заявки застройщиков на подключение к платформе, без входа.
[ApiController]
[Route("api/v1/public")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicApplicationsController(IOrgApplicationService applications) : ControllerBase
{
    /// <summary>Заявка застройщика на подключение к платформе. Повтор с той же почты в течение суток — 409.</summary>
    [HttpPost("applications")]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)]
    [ProducesResponseType<SubmitApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<SubmitApplicationResponse> Submit(SubmitApplicationRequest request, CancellationToken ct) =>
        applications.SubmitAsync(request, ct);
}
