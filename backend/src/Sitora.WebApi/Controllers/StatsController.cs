using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Статистика CRM. Цифры — по домам, которые видит сотрудник: владелец — вся организация, админ объекта — свой дом.
// Сводку и продажи видят сотрудники организации; по домам — руководители; Excel — владелец и бухгалтер.
// Создатель платформы смотрит статистику только в режиме «как владелец», покупатель — никогда.
[ApiController]
[Route("api/v1/stats")]
[Authorize(Roles = Staff)]
[Produces("application/json")]
public class StatsController(IStatsService stats) : ControllerBase
{
    private const string Staff = RoleNames.Owner + "," + RoleNames.ObjectAdmin + "," + RoleNames.Manager + "," + RoleNames.Accountant;
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Квартиры по статусам, стоимость договоров, поступления и остаток, просрочка, заявки и сделки, конверсия.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType<DashboardStatsResponse>(StatusCodes.Status200OK)]
    public Task<DashboardStatsResponse> Dashboard(CancellationToken ct) => stats.GetDashboardAsync(ct);

    /// <summary>По месяцам: сделки и поступления. По умолчанию — последние 6 месяцев, не больше 24.</summary>
    [HttpGet("sales")]
    [ProducesResponseType<IReadOnlyList<MonthlySalesRow>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<IReadOnlyList<MonthlySalesRow>> Sales(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        stats.GetSalesAsync(from, to, ct);

    /// <summary>По домам: квартиры по статусам, занятость, договоры и поступления. Только руководители.</summary>
    [HttpGet("buildings")]
    [Authorize(Roles = RoleNames.Owner + "," + RoleNames.ObjectAdmin)]
    [ProducesResponseType<IReadOnlyList<BuildingStatsRow>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<BuildingStatsRow>> Buildings(CancellationToken ct) => stats.GetBuildingsAsync(ct);

    /// <summary>Excel: сделки, график платежей, поступления за период (по умолчанию — год) и сводка по домам.</summary>
    [HttpGet("export")]
    [Authorize(Roles = RoleNames.Owner + "," + RoleNames.Accountant)]
    [Produces(ExcelType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(DateOnly? from, DateOnly? to, Guid? buildingId, CancellationToken ct) =>
        File(await stats.ExportAsync(from, to, buildingId, ct), ExcelType, $"statistika-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
}
