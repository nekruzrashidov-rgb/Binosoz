using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/rates")]
[Produces("application/json")]
public class RatesController(IExchangeRateService rates, ICatalogService catalog, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Текущий курс: общий курс НБТ или ручной курс организации. Сотрудник получает курс своей организации;
    /// публичный сайт застройщика передаёт organizationId или slug организации (organization).
    /// Курса ещё нет — 404; с allowEmpty=true — 204 без тела (сайт и CRM: «курс не задан» без ошибки в консоли).
    /// </summary>
    [HttpGet("current")]
    [OutputCache(PolicyName = CachingSetup.PublicShort)]
    [AllowAnonymous]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RateResponse>> Current(Currency currency = Currency.USD, Guid? organizationId = null, string? organization = null,
        bool allowEmpty = false, CancellationToken ct = default)
    {
        if (currentUser.OrganizationId is null && organizationId is null && !string.IsNullOrWhiteSpace(organization))
            organizationId = await catalog.FindOrganizationIdAsync(organization, ct)
                             ?? throw new NotFoundException("Организация не найдена");
        var orgId = currentUser.OrganizationId ?? organizationId;
        // Для сомони — та же ошибка «сомони — базовая валюта», что и без allowEmpty.
        if (!allowEmpty || currency == Currency.TJS)
            return await rates.GetCurrentAsync(currency, orgId, ct);
        return await rates.FindCurrentAsync(currency, orgId, ct) is { } rate ? rate : NoContent();
    }

    /// <summary>
    /// Последняя загрузка курса с сайта НБТ (C-09): когда пробовали, ok или причина ошибки, когда загрузили удачно
    /// и на какую дату последний курс НБТ. Для CRM → «Курс».
    /// </summary>
    [HttpGet("status")]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<RateUpdateStatus>(StatusCodes.Status200OK)]
    public Task<RateUpdateStatus> Status(CancellationToken ct) => rates.GetUpdateStatusAsync(ct);

    /// <summary>Ручной ввод курса бухгалтером: действует в его организации; курс на ту же дату перезаписывается.</summary>
    [HttpPut]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<RateResponse>(StatusCodes.Status200OK)]
    public Task<RateResponse> Set(SetRateRequest request, CancellationToken ct) =>
        rates.SetAsync(request, ct);
}
