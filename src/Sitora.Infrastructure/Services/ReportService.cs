using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Infrastructure.Data;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Services;

public class ReportService(AppDbContext db, TimeProvider timeProvider) : IReportService
{
    public const int MaxPeriodDays = 366;
    private const string MoneyFormat = "#,##0.00";

    public async Task<PaymentsReportResponse> GetPaymentsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = Today;
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;
        if (end < start)
            throw new BusinessRuleException("Дата окончания периода раньше даты начала");
        if (end.DayNumber - start.DayNumber >= MaxPeriodDays)
            throw new BusinessRuleException($"Период отчёта — не больше {MaxPeriodDays} дней");

        var rows = await db.Payments.AsNoTracking()
            .Where(p => p.PaidAt >= start && p.PaidAt <= end)
            .OrderBy(p => p.PaidAt).ThenBy(p => p.CreatedAt)
            .Select(p => new PaymentReportRow(
                p.Id, p.PaidAt, p.DealId, p.Deal.Customer.FullName, p.Deal.Apartment.Floor.Building.NameRu,
                p.Deal.Apartment.Number, p.Amount, p.Currency, p.RateUsed, p.AmountTjs, p.Method,
                p.ReversalOfId != null, p.Comment))
            .ToListAsync(ct);

        var totals = rows.GroupBy(r => r.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new MoneyTotal(g.Key, g.Sum(r => r.Amount)))
            .ToList();
        return new PaymentsReportResponse(start, end, rows, totals, rows.Sum(r => r.AmountTjs));
    }

    public async Task<DebtsReportResponse> GetDebtsAsync(DateOnly? asOf, CancellationToken ct = default)
    {
        var date = asOf ?? Today;

        var plans = await db.PaymentPlans.AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.Deal.Stage != DealStage.Cancelled)
            .Select(p => new
            {
                Plan = p,
                p.Deal.Currency,
                Customer = p.Deal.Customer.FullName,
                p.Deal.Customer.Phone,
                Building = p.Deal.Apartment.Floor.Building.NameRu,
                Apartment = p.Deal.Apartment.Number,
                // Оплачено к этой дате; более поздние платежи и сторно не учитываются.
                PaidAsOf = db.Payments.Where(x => x.DealId == p.DealId && x.PaidAt <= date).Sum(x => (decimal?)x.AmountInDealCurrency) ?? 0
            })
            .ToListAsync(ct);

        var rows = new List<DebtReportRow>();
        foreach (var row in plans)
        {
            // Распределение по графику на дату считаем заново: в базе оно хранится на сегодня.
            // План загружен без отслеживания, поэтому изменения не сохранятся.
            row.Plan.ApplyPayments(row.PaidAsOf);
            var overdueItems = row.Plan.Items
                .Where(i => i.DueDate < date && i.AmountPaid < i.AmountDue)
                .OrderBy(i => i.DueDate)
                .ToList();
            if (overdueItems.Count == 0)
                continue;

            var oldest = overdueItems[0].DueDate;
            rows.Add(new DebtReportRow(
                row.Plan.DealId, row.Customer, row.Phone, row.Building, row.Apartment, row.Currency,
                overdueItems.Sum(i => i.AmountDue - i.AmountPaid),
                oldest,
                date.DayNumber - oldest.DayNumber,
                row.Plan.Total - row.PaidAsOf));
        }

        rows = rows.OrderByDescending(r => r.DaysOverdue).ThenByDescending(r => r.Overdue).ToList();
        var totals = rows.GroupBy(r => r.Currency)
            .OrderBy(g => g.Key)
            .Select(g => new MoneyTotal(g.Key, g.Sum(r => r.Overdue)))
            .ToList();
        return new DebtsReportResponse(date, rows, totals);
    }

    public byte[] ToExcel(PaymentsReportResponse report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Поступления");
        sheet.Cell(1, 1).Value = $"Поступления с {report.From:dd.MM.yyyy} по {report.To:dd.MM.yyyy}";
        sheet.Cell(1, 1).Style.Font.Bold = true;

        string[] headers = ["Дата", "Покупатель", "Дом", "Кв.", "Сумма", "Валюта", "Курс", "Сумма, TJS", "Способ", "Вид", "Комментарий"];
        WriteHeader(sheet, 3, headers);

        var r = 4;
        foreach (var row in report.Rows)
        {
            sheet.Cell(r, 1).Value = row.PaidAt.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(r, 2).Value = row.CustomerName;
            sheet.Cell(r, 3).Value = row.BuildingName;
            sheet.Cell(r, 4).Value = row.ApartmentNumber;
            sheet.Cell(r, 5).Value = row.Amount;
            sheet.Cell(r, 6).Value = row.Currency.ToString();
            if (row.RateUsed is { } rate)
                sheet.Cell(r, 7).Value = rate;
            sheet.Cell(r, 8).Value = row.AmountTjs;
            sheet.Cell(r, 9).Value = MethodText(row.Method);
            sheet.Cell(r, 10).Value = row.IsReversal ? "сторно" : "оплата";
            sheet.Cell(r, 11).Value = row.Comment;
            r++;
        }

        r++;
        foreach (var total in report.TotalsByCurrency)
            WriteTotal(sheet, r++, $"Итого в {total.Currency}", 5, total.Amount);
        WriteTotal(sheet, r, "Итого в сомони", 8, report.TotalTjs);

        sheet.Column(1).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Column(5).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(7).Style.NumberFormat.Format = "0.0000";
        sheet.Column(8).Style.NumberFormat.Format = MoneyFormat;
        return Save(workbook, sheet);
    }

    public byte[] ToExcel(DebtsReportResponse report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Задолженность");
        sheet.Cell(1, 1).Value = $"Задолженность на {report.AsOf:dd.MM.yyyy}";
        sheet.Cell(1, 1).Style.Font.Bold = true;

        string[] headers = ["Покупатель", "Телефон", "Дом", "Кв.", "Валюта", "Просрочено", "Просрочка с", "Дней", "Остаток по договору"];
        WriteHeader(sheet, 3, headers);

        var r = 4;
        foreach (var row in report.Rows)
        {
            sheet.Cell(r, 1).Value = row.CustomerName;
            sheet.Cell(r, 2).Value = row.Phone;
            sheet.Cell(r, 3).Value = row.BuildingName;
            sheet.Cell(r, 4).Value = row.ApartmentNumber;
            sheet.Cell(r, 5).Value = row.Currency.ToString();
            sheet.Cell(r, 6).Value = row.Overdue;
            sheet.Cell(r, 7).Value = row.OldestOverdueDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(r, 8).Value = row.DaysOverdue;
            sheet.Cell(r, 9).Value = row.Remaining;
            r++;
        }

        r++;
        foreach (var total in report.TotalsByCurrency)
            WriteTotal(sheet, r++, $"Итого просрочено в {total.Currency}", 6, total.Amount);

        sheet.Column(6).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(7).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Column(9).Style.NumberFormat.Format = MoneyFormat;
        return Save(workbook, sheet);
    }

    private static void WriteHeader(IXLWorksheet sheet, int row, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(row, c + 1).Value = headers[c];
        sheet.Row(row).Style.Font.Bold = true;
    }

    private static void WriteTotal(IXLWorksheet sheet, int row, string title, int column, decimal amount)
    {
        sheet.Cell(row, 1).Value = title;
        sheet.Cell(row, column).Value = amount;
        sheet.Row(row).Style.Font.Bold = true;
    }

    private static byte[] Save(XLWorkbook workbook, IXLWorksheet sheet)
    {
        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string MethodText(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "наличные",
        PaymentMethod.BankTransfer => "перевод",
        PaymentMethod.Card => "карта",
        _ => "другое"
    };

    private DateOnly Today => DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
}
