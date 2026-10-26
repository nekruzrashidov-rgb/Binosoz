using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Produces("application/json")]
public class RatesController(IExchangeRateService rates, ICatalogService catalog, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Текущий курс: общий курс НБТ или ручной курс организации. Сотрудник получает курс своей организации;
    /// публичный сайт застройщика передаёт organizationId или slug организации (organization).
    /// </summary>
    [HttpGet("current")]
    [AllowAnonymous]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<RateResponse> Current(Currency currency = Currency.USD, Guid? organizationId = null, string? organization = null,
        CancellationToken ct = default)
    {
        if (currentUser.OrganizationId is null && organizationId is null && !string.IsNullOrWhiteSpace(organization))
            organizationId = await catalog.FindOrganizationIdAsync(organization, ct)
                             ?? throw new NotFoundException("Организация не найдена");
        return await rates.GetCurrentAsync(currency, currentUser.OrganizationId ?? organizationId, ct);
    }

    /// <summary>Ручной ввод курса бухгалтером: действует в его организации; курс на ту же дату перезаписывается.</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    public Task<RateResponse> Set(SetRateRequest request, CancellationToken ct) =>
        rates.SetAsync(request, ct);
}
