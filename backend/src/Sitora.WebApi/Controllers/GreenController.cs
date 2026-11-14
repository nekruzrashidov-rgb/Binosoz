using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Sitora.Domain.DTOs.Green;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// «Зелёная» часть: настройки застройщика (тарифы и коэффициенты с источниками) и расчёт расходов квартиры.
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class GreenController(IGreenService green) : ControllerBase
{
    /// <summary>Тариф на электроэнергию, «типовой дом» для сравнения, коэффициент CO₂ (с источниками) и признак демо-данных.</summary>
    [HttpGet("owner/green-settings")]
    [Authorize(Roles = Roles.OrganizationLeaders)]
    public Task<GreenSettingsResponse> GetSettings(CancellationToken ct) => green.GetSettingsAsync(ct);

    /// <summary>Сохранить настройки целиком; пустое поле — «не задано», тогда соответствующий расчёт не показывается.</summary>
    [HttpPut("owner/green-settings")]
    [Authorize(Roles = Roles.OrganizationLeaders)]
    public Task<GreenSettingsResponse> SaveSettings(SaveGreenSettingsRequest request, CancellationToken ct) =>
        green.SaveSettingsAsync(request, ct);

    /// <summary>
    /// Ориентировочные расходы на энергию квартиры: площадь × расчётное потребление дома × тариф застройщика.
    /// 204 — у дома нет расчётного потребления или у застройщика нет тарифа (расчёт не показывается).
    /// </summary>
    [HttpGet("apartments/{id:guid}/utility")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CachingSetup.Catalog)]
    [ProducesResponseType<UtilityEstimateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Utility(Guid id, CancellationToken ct) =>
        await green.GetUtilityEstimateAsync(id, ct) is { } estimate ? Ok(estimate) : NoContent();

    /// <summary>
    /// «Зелёный эффект» застройщика для его сайта (organization — slug): документы и PDF, выданные онлайн, сэкономленные
    /// страницы и бумага (0,005 кг на лист A4), CO₂ — только с коэффициентом и источником застройщика, действия без визита
    /// в офис (брони с сайта, онлайн-оплаты, сообщения в чате). Кэш 5 минут.
    /// </summary>
    [HttpGet("public/green-impact")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CachingSetup.GreenImpact)]
    [ProducesResponseType<GreenImpactResponse>(StatusCodes.Status200OK)]
    public Task<GreenImpactResponse> PublicImpact(string? organization, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(organization)
            ? throw new Sitora.Domain.Exceptions.BadRequestException("Укажите организацию")
            : green.GetPublicImpactAsync(organization, ct);

    /// <summary>То же для CRM своей организации — с разбивкой действий и страницами по месяцам за год.</summary>
    [HttpGet("stats/green")]
    [Authorize(Roles = Roles.Sales)]
    public Task<GreenStatsResponse> Stats(CancellationToken ct) => green.GetStatsAsync(ct);
}
