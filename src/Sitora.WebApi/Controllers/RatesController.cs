using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Produces("application/json")]
public class RatesController(IExchangeRateService rates, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Текущий курс: общий курс НБТ или ручной курс организации. Сотрудник получает курс своей организации;
    /// публичный сайт застройщика может передать organizationId.
    /// </summary>
    [HttpGet("current")]
    [AllowAnonymous]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RateResponse> Current(Currency currency = Currency.USD, Guid? organizationId = null, CancellationToken ct = default) =>
        rates.GetCurrentAsync(currency, currentUser.OrganizationId ?? organizationId, ct);

    /// <summary>Ручной ввод курса бухгалтером: действует в его организации; курс на ту же дату перезаписывается.</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    public Task<RateResponse> Set(SetRateRequest request, CancellationToken ct) =>
        rates.SetAsync(request, ct);
}
