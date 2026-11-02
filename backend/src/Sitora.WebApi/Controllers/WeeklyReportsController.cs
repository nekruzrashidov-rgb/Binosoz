using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Еженедельные отчёты по дому: смотрят бухгалтер и руководители, вручную формирует владелец.
// Чужой дом или отчёт чужого дома — 404 (фильтры домов в AppDbContext).
[ApiController]
[Route("api/v1/buildings/{buildingId:guid}/reports/weekly")]
[Authorize(Roles = Roles.Accounting)]
[Produces("application/json")]
public class WeeklyReportsController(IWeeklyReportService reports) : ControllerBase
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Отчёты дома, новые сверху. Формируются по понедельникам в 09:00 по Душанбе за прошедшую неделю пн–вс.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<WeeklyReportResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<WeeklyReportResponse>> List(Guid buildingId, CancellationToken ct) =>
        reports.ListAsync(buildingId, ct);

    /// <summary>Excel: листы «Сводка», «Платежи», «Касса», «Просрочки».</summary>
    [HttpGet("{reportId:guid}/excel")]
    [Produces(ExcelType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Excel(Guid buildingId, Guid reportId, CancellationToken ct) =>
        File(await reports.GetExcelAsync(buildingId, reportId, ct), ExcelType, "otchet-za-nedelyu.xlsx");

    /// <summary>PDF A4: сводка и топ-5 должников.</summary>
    [HttpGet("{reportId:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid buildingId, Guid reportId, CancellationToken ct) =>
        File(await reports.GetPdfAsync(buildingId, reportId, ct), "application/pdf", "otchet-za-nedelyu.pdf");

    /// <summary>Сформировать отчёт за последнюю завершённую неделю сейчас; готовый за ту же неделю перезаписывается.</summary>
    [HttpPost("generate")]
    [Authorize(Roles = Roles.OrganizationLeaders)]
    [ProducesResponseType<WeeklyReportResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WeeklyReportResponse>> Generate(Guid buildingId, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reports.GenerateAsync(buildingId, ct));
}
