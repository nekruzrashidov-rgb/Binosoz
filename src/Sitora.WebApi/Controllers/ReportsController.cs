using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Отчёты бухгалтера. Без пагинации: объём ограничен периодом (не больше года) или датой.
[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = Roles.Accounting)]
[Produces("application/json")]
public class ReportsController(IReportService reports) : ControllerBase
{
    private const string ExcelType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Поступления за период (по умолчанию — текущий месяц): платежи и сторно, итоги по валютам.</summary>
    [HttpGet("payments")]
    [ProducesResponseType<PaymentsReportResponse>(StatusCodes.Status200OK)]
    public Task<PaymentsReportResponse> Payments(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        reports.GetPaymentsAsync(from, to, ct);

    [HttpGet("payments.xlsx")]
    [Produces(ExcelType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PaymentsExcel(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var report = await reports.GetPaymentsAsync(from, to, ct);
        return File(reports.ToExcel(report), ExcelType, $"postupleniya-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.xlsx");
    }

    /// <summary>Задолженность на дату (по умолчанию — сегодня): сделки с просрочкой по графику.</summary>
    [HttpGet("debts")]
    [ProducesResponseType<DebtsReportResponse>(StatusCodes.Status200OK)]
    public Task<DebtsReportResponse> Debts(DateOnly? asOf, CancellationToken ct) =>
        reports.GetDebtsAsync(asOf, ct);

    [HttpGet("debts.xlsx")]
    [Produces(ExcelType)]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> DebtsExcel(DateOnly? asOf, CancellationToken ct)
    {
        var report = await reports.GetDebtsAsync(asOf, ct);
        return File(reports.ToExcel(report), ExcelType, $"zadolzhennost-{report.AsOf:yyyy-MM-dd}.xlsx");
    }
}
