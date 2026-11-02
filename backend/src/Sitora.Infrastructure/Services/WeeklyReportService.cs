using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Еженедельный отчёт владельцу: касса, новые сделки, платежи, просрочки и стройка за неделю пн–вс по Душанбе.
// Какие дома видны, решают фильтры организации и домов в AppDbContext: чужой дом — 404.
// Из фоновой задачи пользователя нет, фильтры не действуют — видны все дома.
public class WeeklyReportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditService audit,
    IPdfGenerator pdf,
    TimeProvider timeProvider,
    ILogger<WeeklyReportService> logger) : IWeeklyReportService
{
    private const string BuildingNotFound = "Дом не найден";
    private const string ReportNotFound = "Отчёт не найден";
    private const string MoneyFormat = "#,##0.00";
    public const int TopDebtors = 5;

    public async Task<IReadOnlyList<WeeklyReportResponse>> ListAsync(Guid buildingId, CancellationToken ct = default)
    {
        await FindBuildingAsync(buildingId, ct);
        return await db.WeeklyReports.AsNoTracking()
            .Where(r => r.BuildingId == buildingId)
            .OrderByDescending(r => r.PeriodStart)
            .Select(r => new WeeklyReportResponse(r.Id, r.BuildingId, r.PeriodStart, r.PeriodEnd, r.GeneratedAt, r.GeneratedById != null))
            .ToListAsync(ct);
    }

    public async Task<byte[]> GetExcelAsync(Guid buildingId, Guid reportId, CancellationToken ct = default) =>
        await db.WeeklyReports.AsNoTracking().Where(r => r.Id == reportId && r.BuildingId == buildingId)
            .Select(r => r.Excel).FirstOrDefaultAsync(ct) ?? throw new NotFoundException(ReportNotFound);

    public async Task<byte[]> GetPdfAsync(Guid buildingId, Guid reportId, CancellationToken ct = default) =>
        await db.WeeklyReports.AsNoTracking().Where(r => r.Id == reportId && r.BuildingId == buildingId)
            .Select(r => r.Pdf).FirstOrDefaultAsync(ct) ?? throw new NotFoundException(ReportNotFound);

    public async Task<WeeklyReportResponse> GenerateAsync(Guid buildingId, CancellationToken ct = default)
    {
        var building = await FindBuildingAsync(buildingId, ct);
        var report = await SaveAsync(building, LastWeek(), currentUser.UserId, ct);
        return new WeeklyReportResponse(report.Id, report.BuildingId, report.PeriodStart, report.PeriodEnd, report.GeneratedAt, report.GeneratedById != null);
    }

    public async Task<int> GenerateForAllAsync(CancellationToken ct = default)
    {
        var (start, end) = LastWeek();
        var ownerRole = await db.Roles.Where(r => r.Name == RoleNames.Owner).Select(r => r.Id).SingleAsync(ct);
        var buildings = await db.Buildings.AsNoTracking()
            .Where(b => db.Users.Any(u => u.IsActive && u.OrganizationId == b.OrganizationId
                                          && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == ownerRole)))
            .Where(b => !db.WeeklyReports.Any(r => r.BuildingId == b.Id && r.PeriodStart == start))
            .ToListAsync(ct);

        foreach (var building in buildings)
            await SaveAsync(building, (start, end), null, ct);
        if (buildings.Count > 0)
            logger.LogInformation("Еженедельных отчётов сформировано: {Count} за {Start:dd.MM.yyyy}–{End:dd.MM.yyyy}", buildings.Count, start, end);
        return buildings.Count;
    }

    // Отчёт за неделю пересобирается целиком; тот, что уже был за эту неделю, заменяется.
    private async Task<WeeklyReport> SaveAsync(Building building, (DateOnly Start, DateOnly End) period, Guid? generatedBy, CancellationToken ct)
    {
        var data = await CollectAsync(building, period.Start, period.End, ct);
        var report = await db.WeeklyReports.FirstOrDefaultAsync(r => r.BuildingId == building.Id && r.PeriodStart == period.Start, ct);
        if (report is null)
        {
            report = new WeeklyReport { BuildingId = building.Id, PeriodStart = period.Start, PeriodEnd = period.End };
            db.WeeklyReports.Add(report);
        }
        report.GeneratedAt = data.GeneratedAtUtc;
        report.GeneratedById = generatedBy;
        report.Excel = RenderExcel(data);
        report.Pdf = pdf.RenderWeeklyReport(data);

        audit.Record(nameof(WeeklyReport), report.Id, "Generate", null,
            new { BuildingId = building.Id, period.Start, period.End, Manual = generatedBy is not null }, building.OrganizationId);
        await db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<WeeklyReportData> CollectAsync(Building building, DateOnly start, DateOnly end, CancellationToken ct)
    {
        var asOf = end.AddDays(1);
        // Границы недели в UTC: сделки хранят время создания в UTC, неделя считается по Душанбе.
        var fromUtc = start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DushanbeTime.Offset;
        var toUtc = asOf.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DushanbeTime.Offset;
        var organization = await db.Organizations.AsNoTracking().Where(o => o.Id == building.OrganizationId)
            .Select(o => o.Name).FirstOrDefaultAsync(ct) ?? "";

        var cash = await db.CashTransactions.AsNoTracking()
            .Where(t => t.BuildingId == building.Id && t.OperationDate >= start && t.OperationDate <= end)
            .OrderBy(t => t.OperationDate).ThenBy(t => t.CreatedAt)
            .Select(t => new { t.OperationDate, t.Category, t.Direction, t.Amount, t.Currency, t.AmountTjs, t.Description })
            .ToListAsync(ct);
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.Deal.Apartment.Floor.BuildingId == building.Id && p.PaidAt >= start && p.PaidAt <= end)
            .OrderBy(p => p.PaidAt).ThenBy(p => p.CreatedAt)
            .Select(p => new { p.PaidAt, p.Deal.Customer.FullName, p.Deal.Apartment.Number, p.Amount, p.Currency, p.Method, p.ReversalOfId })
            .ToListAsync(ct);
        var newDeals = await db.Deals.CountAsync(d => d.Apartment.Floor.BuildingId == building.Id
                                                      && d.CreatedAt >= fromUtc && d.CreatedAt < toUtc, ct);
        var overdue = await db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.Plan.Deal.Apartment.Floor.BuildingId == building.Id && i.DueDate < asOf && i.AmountPaid < i.AmountDue
                        && i.Plan.Deal.Stage != DealStage.Cancelled && i.Plan.Deal.Stage != DealStage.Completed)
            .OrderBy(i => i.DueDate)
            .Select(i => new { i.Plan.DealId, i.Plan.Deal.Customer.FullName, i.Plan.Deal.Apartment.Number, i.DueDate,
                Left = i.AmountDue - i.AmountPaid, i.Plan.Deal.Currency })
            .ToListAsync(ct);
        var stages = await db.ConstructionStages.AsNoTracking().Where(s => s.BuildingId == building.Id).Select(s => s.Percent).ToListAsync(ct);

        // Сторно учтено со своим знаком: приход — сумма приходных записей, расход — сумма расходных (они отрицательные).
        var income = cash.Where(t => t.Direction == CashDirection.Income).Sum(t => t.AmountTjs);
        var expense = -cash.Where(t => t.Direction == CashDirection.Expense).Sum(t => t.AmountTjs);
        var summary = new WeeklySummary(organization, building.NameRu, start, end, asOf, income, expense, income - expense,
            newDeals, overdue.Count, stages.Count == 0 ? 0 : (int)Math.Round(stages.Average(), MidpointRounding.AwayFromZero));

        var overdueRows = overdue.Select(i => new WeeklyOverdueRow(i.FullName, i.Number, i.DueDate, i.Left, i.Currency, asOf.DayNumber - i.DueDate.DayNumber)).ToList();
        var debtors = overdue.GroupBy(i => i.DealId)
            .Select(g => new WeeklyDebtorRow(g.First().FullName, g.First().Number, g.Sum(i => i.Left), g.First().Currency,
                asOf.DayNumber - g.Min(i => i.DueDate).DayNumber))
            .OrderByDescending(d => d.MaxDaysOverdue).ThenByDescending(d => d.Left)
            .Take(TopDebtors).ToList();

        return new WeeklyReportData(summary,
            payments.Select(p => new WeeklyPaymentRow(p.PaidAt, p.FullName, p.Number, p.Amount, p.Currency, ReportService.MethodText(p.Method), p.ReversalOfId != null)).ToList(),
            cash.Select(t => new WeeklyCashRow(t.OperationDate, CashService.CategoryText(t.Category), t.Direction, t.Amount, t.Currency, t.AmountTjs, t.Description)).ToList(),
            overdueRows, debtors, timeProvider.GetUtcNow().UtcDateTime);
    }

    // Последняя завершённая неделя: с понедельника по воскресенье перед текущей неделей.
    public (DateOnly Start, DateOnly End) LastWeek()
    {
        var today = DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        return (monday.AddDays(-7), monday.AddDays(-1));
    }

    public static byte[] RenderExcel(WeeklyReportData data)
    {
        var s = data.Summary;
        using var workbook = new XLWorkbook();

        var summary = workbook.AddWorksheet("Сводка");
        summary.Cell(1, 1).Value = $"Еженедельный отчёт: {s.BuildingName}";
        summary.Cell(1, 1).Style.Font.Bold = true;
        summary.Cell(1, 1).Style.Font.FontSize = 14;
        (string Title, XLCellValue Value)[] lines =
        [
            ("Организация", s.OrganizationName),
            ("Период", $"{s.PeriodStart:dd.MM.yyyy} — {s.PeriodEnd:dd.MM.yyyy}"),
            ("Сформирован", $"{DushanbeTime.ToLocal(data.GeneratedAtUtc):dd.MM.yyyy HH:mm} (Душанбе)"),
            ("Поступления по кассе, TJS", s.IncomeTjs),
            ("Расходы по кассе, TJS", s.ExpenseTjs),
            ("Чистая прибыль, TJS", s.ProfitTjs),
            ("Новых сделок", s.NewDeals),
            ($"Просроченных платежей на {s.AsOf:dd.MM.yyyy}", s.OverdueItems),
            ("Готовность стройки, %", s.ConstructionPercent),
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            summary.Cell(i + 3, 1).Value = lines[i].Title;
            summary.Cell(i + 3, 1).Style.Font.Bold = true;
            summary.Cell(i + 3, 2).Value = lines[i].Value;
        }
        summary.Range(6, 2, 8, 2).Style.NumberFormat.Format = MoneyFormat;
        summary.Columns().AdjustToContents();

        Table(workbook.AddWorksheet("Платежи"), ["Дата", "Покупатель", "Квартира", "Сумма", "Валюта", "Способ", "Тип"],
            data.Payments, (row, p) => [p.PaidAt.ToDateTime(TimeOnly.MinValue), p.Customer, p.Apartment, p.Amount, p.Currency.ToString(), p.Method, p.IsReversal ? "сторно" : "оплата"],
            dateColumns: [1], moneyColumns: [4]);
        Table(workbook.AddWorksheet("Касса"), ["Дата", "Категория", "Направление", "Сумма", "Валюта", "В сомони", "Описание"],
            data.Cash, (row, t) => [t.Date.ToDateTime(TimeOnly.MinValue), t.Category, t.Direction == CashDirection.Income ? "Приход" : "Расход", t.Amount, t.Currency.ToString(), t.AmountTjs, t.Description],
            dateColumns: [1], moneyColumns: [4, 6]);
        Table(workbook.AddWorksheet("Просрочки"), ["Покупатель", "Квартира", "Срок", "Сумма", "Валюта", "Просрочено, дн."],
            data.Overdue, (row, o) => [o.Customer, o.Apartment, o.DueDate.ToDateTime(TimeOnly.MinValue), o.Left, o.Currency.ToString(), o.DaysOverdue],
            dateColumns: [3], moneyColumns: [4]);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Table<T>(IXLWorksheet sheet, string[] headers, IReadOnlyList<T> rows, Func<int, T, XLCellValue[]> cells,
        int[] dateColumns, int[] moneyColumns)
    {
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(1, c + 1).Value = headers[c];
        sheet.Row(1).Style.Font.Bold = true;
        if (rows.Count == 0)
            sheet.Cell(2, 1).Value = "За неделю записей нет";
        for (var r = 0; r < rows.Count; r++)
        {
            var values = cells(r, rows[r]);
            for (var c = 0; c < values.Length; c++)
                sheet.Cell(r + 2, c + 1).Value = values[c];
        }
        foreach (var c in dateColumns)
            sheet.Column(c).Style.DateFormat.Format = "dd.MM.yyyy";
        foreach (var c in moneyColumns)
            sheet.Column(c).Style.NumberFormat.Format = MoneyFormat;
        sheet.Columns().AdjustToContents();
    }

    private async Task<Building> FindBuildingAsync(Guid buildingId, CancellationToken ct) =>
        await db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct)
            ?? throw new NotFoundException(BuildingNotFound);
}
