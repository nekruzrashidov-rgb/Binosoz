using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Produces("application/json")]
public class RatesController(IExchangeRateService rates) : ControllerBase
{
    [HttpGet("current")]
    [AllowAnonymous]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RateResponse> Current(Currency currency = Currency.USD, CancellationToken ct = default) =>
        rates.GetCurrentAsync(currency, ct);

    /// <summary>Ручной ввод курса НБТ бухгалтером; курс на ту же дату перезаписывается.</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    public Task<RateResponse> Set(SetRateRequest request, CancellationToken ct) =>
        rates.SetAsync(request, ct);
}
