using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Тарифы платформы — только суперадмин. Лимиты тарифа (дома, сотрудники) проверяются при создании дома и сотрудника: 402.
[ApiController]
[Route("api/v1/platform")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public class PlatformTariffsController(ITariffService tariffs) : ControllerBase
{
    /// <summary>Все тарифы с числом организаций, у которых тариф действует сейчас.</summary>
    [HttpGet("tariffs")]
    public Task<IReadOnlyList<TariffPlanResponse>> List(CancellationToken ct) => tariffs.GetAsync(ct);

    /// <summary>Новый тариф: название (уникальное), цена, валюта, лимиты домов и сотрудников (пусто — без ограничения), возможности.</summary>
    [HttpPost("tariffs")]
    [ProducesResponseType<TariffPlanResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TariffPlanResponse>> Create(SaveTariffRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tariffs.CreateAsync(request, ct));

    /// <summary>Изменить тариф; отключённый (isActive = false) нельзя назначить, но у кого он уже есть — остаётся.</summary>
    [HttpPut("tariffs/{id:guid}")]
    [ProducesResponseType<TariffPlanResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TariffPlanResponse> Update(Guid id, SaveTariffRequest request, CancellationToken ct) =>
        tariffs.UpdateAsync(id, request, ct);

    /// <summary>Назначить тариф организации с текущего момента (endsAt — до какого числа, пусто — бессрочно). Прежний тариф закрывается.</summary>
    [HttpPost("organizations/{organizationId:guid}/tariff")]
    [ProducesResponseType<OrganizationTariffResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<OrganizationTariffResponse> Assign(Guid organizationId, AssignTariffRequest request, CancellationToken ct) =>
        tariffs.AssignAsync(organizationId, request, ct);
}
