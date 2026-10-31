using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Статистика CRM. Что видит сотрудник, решают фильтры организации и домов в AppDbContext: владелец — всю организацию,
// админ объекта и сотрудники с домами — только свои дома. Результаты кэшируются на 5 минут (StatsCache).
// Платежи не удаляются: ошибку исправляет сторно с отрицательной суммой, поэтому «поступило» — сумма всех записей.
public class StatsService(
    AppDbContext db,
    StatsCache cache,
    ICurrentUser currentUser,
    IExchangeRateService rates,
    TimeProvider timeProvider) : IStatsService
{
    public const int MaxSalesMonths = 24;
    private static readonly DealStage[] SoldStages = [DealStage.Contract, DealStage.Payment, DealStage.Completed];
    private const string MoneyFormat = "#,##0.00";

    public Task<DashboardStatsResponse> GetDashboardAsync(CancellationToken ct = default) =>
        Cached("dashboard", () => BuildDashboardAsync(ct), ct);

    public Task<IReadOnlyList<MonthlySalesRow>> GetSalesAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var today = Today;
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1).AddMonths(-5);
        if (end < start)
            throw new BusinessRuleException("Дата окончания периода раньше даты начала");
        var firstMonth = new DateOnly(start.Year, start.Month, 1);
        var months = (end.Year - firstMonth.Year) * 12 + end.Month - firstMonth.Month + 1;
        if (months > MaxSalesMonths)
            throw new BusinessRuleException($"Период — не больше {MaxSalesMonths} месяцев");
        return Cached($"sales:{start:yyyyMMdd}:{end:yyyyMMdd}", () => BuildSalesAsync(start, end, firstMonth, months, ct), ct);
    }

    public Task<IReadOnlyList<BuildingStatsRow>> GetBuildingsAsync(CancellationToken ct = default) =>
        Cached($"buildings:{(IsTajik ? "tg" : "ru")}", () => BuildBuildingsAsync(ct), ct);

    private async Task<DashboardStatsResponse> BuildDashboardAsync(CancellationToken ct)
    {
        var today = Today;
        var statuses = await db.Apartments.GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var usdRate = await UsdRateAsync(ct);
        var sold = await db.Deals.Where(d => SoldStages.Contains(d.Stage))
            .GroupBy(d => d.Currency)
            .Select(g => new MoneyTotal(g.Key, g.Sum(d => d.Price - d.Discount)))
            .ToListAsync(ct);
        var paid = await db.Payments.Where(p => SoldStages.Contains(p.Deal.Stage))
            .GroupBy(p => p.Deal.Currency)
            .Select(g => new { Currency = g.Key, Amount = g.Sum(p => p.AmountInDealCurrency), Tjs = g.Sum(p => p.AmountTjs) })
            .ToListAsync(ct);
        var collected = paid.Select(p => new MoneyTotal(p.Currency, p.Amount)).ToList();
        var remaining = sold.Select(s => new MoneyTotal(s.Currency, s.Amount - (paid.FirstOrDefault(p => p.Currency == s.Currency)?.Amount ?? 0)))
            .ToList();

        // Просрочка — пункты графика со сроком раньше сегодняшнего дня, оплаченные не полностью, по неотменённым сделкам.
        var overdue = await db.PaymentScheduleItems
            .Where(i => i.DueDate < today && i.AmountPaid < i.AmountDue && i.Plan.Deal.Stage != DealStage.Cancelled)
            .Select(i => i.Plan.DealId)
            .ToListAsync(ct);

        var monthStart = UtcStart(new DateOnly(today.Year, today.Month, 1));
        var weekStart = UtcStart(today.AddDays(-6));
        var leadsThisWeek = await db.Leads.CountAsync(l => l.CreatedAt >= weekStart, ct);
        var leadsThisMonth = await db.Leads.CountAsync(l => l.CreatedAt >= monthStart, ct);
        var dealsThisMonth = await db.Deals.CountAsync(d => d.CreatedAt >= monthStart && d.Stage != DealStage.Cancelled, ct);

        return new DashboardStatsResponse(
            statuses.Values.Sum(),
            statuses.GetValueOrDefault(ApartmentStatus.Free),
            statuses.GetValueOrDefault(ApartmentStatus.Booked),
            statuses.GetValueOrDefault(ApartmentStatus.Sold),
            statuses.GetValueOrDefault(ApartmentStatus.OnRequest),
            Summary(sold, usdRate),
            new MoneySummary(Ordered(collected), paid.Sum(p => p.Tjs)),
            Summary(remaining, usdRate),
            overdue.Count,
            overdue.Distinct().Count(),
            leadsThisWeek,
            leadsThisMonth,
            dealsThisMonth,
            leadsThisMonth == 0 ? 0 : Math.Round((decimal)dealsThisMonth / leadsThisMonth, 4),
            usdRate);
    }

    private async Task<IReadOnlyList<MonthlySalesRow>> BuildSalesAsync(DateOnly start, DateOnly end, DateOnly firstMonth, int months, CancellationToken ct)
    {
        var from = UtcStart(start);
        var to = UtcStart(end.AddDays(1));
        var deals = await db.Deals.Where(d => d.CreatedAt >= from && d.CreatedAt < to && d.Stage != DealStage.Cancelled)
            .Select(d => d.CreatedAt)
            .ToListAsync(ct);
        var payments = await db.Payments.Where(p => p.PaidAt >= start && p.PaidAt <= end)
            .Select(p => new { p.PaidAt, p.Currency, p.Amount, p.AmountTjs })
            .ToListAsync(ct);

        var rows = new List<MonthlySalesRow>(months);
        for (var m = 0; m < months; m++)
        {
            var month = firstMonth.AddMonths(m);
            var inMonth = payments.Where(p => p.PaidAt.Year == month.Year && p.PaidAt.Month == month.Month).ToList();
            rows.Add(new MonthlySalesRow(
                month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                deals.Count(d => { var local = DushanbeTime.ToLocal(d); return local.Year == month.Year && local.Month == month.Month; }),
                new MoneySummary(
                    Ordered(inMonth.GroupBy(p => p.Currency).Select(g => new MoneyTotal(g.Key, g.Sum(p => p.Amount)))),
                    inMonth.Sum(p => p.AmountTjs))));
        }

        return rows;
    }

    private async Task<IReadOnlyList<BuildingStatsRow>> BuildBuildingsAsync(CancellationToken ct)
    {
        var tg = IsTajik;
        var buildings = await db.Buildings.AsNoTracking()
            .OrderBy(b => b.NameRu)
            .Select(b => new { b.Id, Name = tg ? b.NameTg : b.NameRu })
            .ToListAsync(ct);
        var statuses = await db.Apartments
            .GroupBy(a => new { a.Floor.BuildingId, a.Status })
            .Select(g => new { g.Key.BuildingId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        var sold = await db.Deals.Where(d => SoldStages.Contains(d.Stage))
            .GroupBy(d => new { d.Apartment.Floor.BuildingId, d.Currency })
            .Select(g => new { g.Key.BuildingId, g.Key.Currency, Amount = g.Sum(d => d.Price - d.Discount) })
            .ToListAsync(ct);
        var paid = await db.Payments.Where(p => SoldStages.Contains(p.Deal.Stage))
            .GroupBy(p => new { p.Deal.Apartment.Floor.BuildingId, p.Deal.Currency })
            .Select(g => new { g.Key.BuildingId, g.Key.Currency, Amount = g.Sum(p => p.AmountInDealCurrency), Tjs = g.Sum(p => p.AmountTjs) })
            .ToListAsync(ct);
        var usdRate = await UsdRateAsync(ct);

        return buildings.Select(b =>
        {
            var own = statuses.Where(s => s.BuildingId == b.Id).ToList();
            int Count(ApartmentStatus s) => own.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
            var total = own.Sum(x => x.Count);
            var booked = Count(ApartmentStatus.Booked);
            var soldCount = Count(ApartmentStatus.Sold);
            var buildingPaid = paid.Where(p => p.BuildingId == b.Id).ToList();
            return new BuildingStatsRow(b.Id, b.Name, total, Count(ApartmentStatus.Free), booked, soldCount, Count(ApartmentStatus.OnRequest),
                total == 0 ? 0 : Math.Round((decimal)(booked + soldCount) / total, 4),
                Summary(sold.Where(s => s.BuildingId == b.Id).Select(s => new MoneyTotal(s.Currency, s.Amount)).ToList(), usdRate),
                new MoneySummary(Ordered(buildingPaid.Select(p => new MoneyTotal(p.Currency, p.Amount))), buildingPaid.Sum(p => p.Tjs)));
        }).ToList();
    }

    // ---------- Excel ----------

    public async Task<byte[]> ExportAsync(DateOnly? from, DateOnly? to, Guid? buildingId, CancellationToken ct = default)
    {
        var end = to ?? Today;
        var start = from ?? end.AddDays(-(ReportService.MaxPeriodDays - 1));
        if (end < start)
            throw new BusinessRuleException("Дата окончания периода раньше даты начала");
        if (end.DayNumber - start.DayNumber >= ReportService.MaxPeriodDays)
            throw new BusinessRuleException($"Период выгрузки — не больше {ReportService.MaxPeriodDays} дней");
        if (buildingId is { } id && !await db.Buildings.AnyAsync(b => b.Id == id, ct))
            throw new NotFoundException("Дом не найден");

        var fromUtc = UtcStart(start);
        var toUtc = UtcStart(end.AddDays(1));
        var dealsQuery = db.Deals.AsNoTracking().Where(d => d.CreatedAt >= fromUtc && d.CreatedAt < toUtc);
        var paymentsQuery = db.Payments.AsNoTracking().Where(p => p.PaidAt >= start && p.PaidAt <= end);
        if (buildingId is { } only)
        {
            dealsQuery = dealsQuery.Where(d => d.Apartment.Floor.BuildingId == only);
            paymentsQuery = paymentsQuery.Where(p => p.Deal.Apartment.Floor.BuildingId == only);
        }

        var deals = await dealsQuery.OrderBy(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id, d.CreatedAt, Buyer = d.Customer.FullName, d.Customer.Phone, Building = d.Apartment.Floor.Building.NameRu,
                Apartment = d.Apartment.Number, Floor = d.Apartment.Floor.Number, d.Apartment.Area, Price = d.Price - d.Discount, d.Currency, d.Stage
            })
            .ToListAsync(ct);
        var dealIds = deals.Select(d => d.Id).ToList();
        var schedule = await db.PaymentScheduleItems.AsNoTracking()
            .Where(i => dealIds.Contains(i.Plan.DealId))
            .OrderBy(i => i.Plan.DealId).ThenBy(i => i.DueDate).ThenBy(i => i.Kind)
            .Select(i => new { i.Plan.DealId, Buyer = i.Plan.Deal.Customer.FullName, i.DueDate, i.AmountDue, i.AmountPaid, i.Plan.Deal.Currency })
            .ToListAsync(ct);
        var payments = await paymentsQuery.OrderBy(p => p.PaidAt).ThenBy(p => p.CreatedAt)
            .Select(p => new { p.DealId, p.PaidAt, Buyer = p.Deal.Customer.FullName, Building = p.Deal.Apartment.Floor.Building.NameRu, Apartment = p.Deal.Apartment.Number, p.Amount, p.Currency, p.RateUsed, p.AmountTjs, IsReversal = p.ReversalOfId != null })
            .ToListAsync(ct);
        var buildings = (await GetBuildingsAsync(ct)).Where(b => buildingId is null || b.BuildingId == buildingId).ToList();
        var today = Today;

        using var workbook = new XLWorkbook();

        var sheet = workbook.AddWorksheet("Сделки");
        Title(sheet, $"Сделки с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}");
        Header(sheet, ["Сделка", "Покупатель", "Телефон", "Дом", "Кв.", "Этаж", "Площадь, м²", "Цена", "Валюта", "Этап", "Дата"]);
        var r = 4;
        foreach (var d in deals)
        {
            sheet.Cell(r, 1).Value = d.Id.ToString();
            sheet.Cell(r, 2).Value = d.Buyer;
            sheet.Cell(r, 3).Value = d.Phone;
            sheet.Cell(r, 4).Value = d.Building;
            sheet.Cell(r, 5).Value = d.Apartment;
            sheet.Cell(r, 6).Value = d.Floor;
            sheet.Cell(r, 7).Value = d.Area;
            sheet.Cell(r, 8).Value = d.Price;
            sheet.Cell(r, 9).Value = d.Currency.ToString();
            sheet.Cell(r, 10).Value = StageText(d.Stage);
            sheet.Cell(r, 11).Value = DushanbeTime.ToLocal(d.CreatedAt).Date;
            r++;
        }
        sheet.Column(8).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(11).Style.DateFormat.Format = "dd.MM.yyyy";
        Fit(sheet);

        sheet = workbook.AddWorksheet("График платежей");
        Title(sheet, "График платежей по этим сделкам");
        Header(sheet, ["Сделка", "Покупатель", "№", "Срок", "К оплате", "Оплачено", "Валюта", "Статус"]);
        r = 4;
        foreach (var deal in schedule.GroupBy(i => i.DealId))
        {
            var n = 1;
            foreach (var i in deal)
            {
                sheet.Cell(r, 1).Value = i.DealId.ToString();
                sheet.Cell(r, 2).Value = i.Buyer;
                sheet.Cell(r, 3).Value = n++;
                sheet.Cell(r, 4).Value = i.DueDate.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(r, 5).Value = i.AmountDue;
                sheet.Cell(r, 6).Value = i.AmountPaid;
                sheet.Cell(r, 7).Value = i.Currency.ToString();
                sheet.Cell(r, 8).Value = i.AmountPaid >= i.AmountDue ? "оплачен" : i.DueDate < today ? "просрочен" : i.AmountPaid > 0 ? "частично" : "ожидается";
                r++;
            }
        }
        sheet.Column(4).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Columns(5, 6).Style.NumberFormat.Format = MoneyFormat;
        Fit(sheet);

        sheet = workbook.AddWorksheet("Поступления");
        Title(sheet, $"Поступления с {start:dd.MM.yyyy} по {end:dd.MM.yyyy}");
        Header(sheet, ["Сделка", "Дата", "Покупатель", "Дом", "Кв.", "Сумма", "Валюта", "Курс", "Сумма, TJS", "Вид"]);
        r = 4;
        foreach (var p in payments)
        {
            sheet.Cell(r, 1).Value = p.DealId.ToString();
            sheet.Cell(r, 2).Value = p.PaidAt.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(r, 3).Value = p.Buyer;
            sheet.Cell(r, 4).Value = p.Building;
            sheet.Cell(r, 5).Value = p.Apartment;
            sheet.Cell(r, 6).Value = p.Amount;
            sheet.Cell(r, 7).Value = p.Currency.ToString();
            if (p.RateUsed is { } rate)
                sheet.Cell(r, 8).Value = rate;
            sheet.Cell(r, 9).Value = p.AmountTjs;
            sheet.Cell(r, 10).Value = p.IsReversal ? "сторно" : "оплата";
            r++;
        }
        sheet.Column(2).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Column(6).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(8).Style.NumberFormat.Format = "0.0000";
        sheet.Column(9).Style.NumberFormat.Format = MoneyFormat;
        Fit(sheet);

        sheet = workbook.AddWorksheet("Сводка");
        Title(sheet, $"Сводка по домам на {today:dd.MM.yyyy}");
        Header(sheet, ["Дом", "Всего", "Свободно", "Бронь", "Продано", "По запросу", "Договоры, TJS", "Поступило, TJS"]);
        r = 4;
        foreach (var b in buildings)
        {
            sheet.Cell(r, 1).Value = b.Name;
            sheet.Cell(r, 2).Value = b.TotalApartments;
            sheet.Cell(r, 3).Value = b.Free;
            sheet.Cell(r, 4).Value = b.Booked;
            sheet.Cell(r, 5).Value = b.Sold;
            sheet.Cell(r, 6).Value = b.OnRequest;
            if (b.Revenue.Tjs is { } revenue)
                sheet.Cell(r, 7).Value = revenue;
            sheet.Cell(r, 8).Value = b.Collected.Tjs ?? 0;
            r++;
        }
        sheet.Columns(7, 8).Style.NumberFormat.Format = MoneyFormat;
        sheet.Cell(r + 1, 1).Value = "Договоры в сомони — по текущему курсу USD; поступления — по курсу на дату платежа.";
        Fit(sheet);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Title(IXLWorksheet sheet, string title)
    {
        sheet.Cell(1, 1).Value = title;
        sheet.Cell(1, 1).Style.Font.Bold = true;
    }

    private static void Header(IXLWorksheet sheet, string[] headers)
    {
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(3, c + 1).Value = headers[c];
        sheet.Row(3).Style.Font.Bold = true;
    }

    private static void Fit(IXLWorksheet sheet) => sheet.Columns().AdjustToContents();

    private static string StageText(DealStage stage) => stage switch
    {
        DealStage.Lead => "заявка",
        DealStage.Showing => "показ",
        DealStage.Booking => "бронь",
        DealStage.Contract => "договор",
        DealStage.Payment => "оплата",
        DealStage.Completed => "завершена",
        _ => "отменена"
    };

    // ---------- общее ----------

    // Ключ учитывает, кто смотрит: у сотрудника с частью домов цифры свои.
    private Task<T> Cached<T>(string key, Func<Task<T>> factory, CancellationToken ct)
    {
        var scope = currentUser.OrganizationId is { } org
            ? $"{org}:{string.Join(',', currentUser.BuildingIds ?? [])}"
            : "all";
        return cache.GetOrCreateAsync($"{scope}:{key}", factory, ct);
    }

    private async Task<decimal?> UsdRateAsync(CancellationToken ct) =>
        (await rates.FindCurrentAsync(Currency.USD, currentUser.OrganizationId, ct))?.RateToTjs;

    // Сумма в сомони: USD — по курсу; нет курса при суммах в USD — не считаем, чтобы не показывать неверное число.
    private static MoneySummary Summary(IReadOnlyList<MoneyTotal> totals, decimal? usdRate)
    {
        var usd = totals.Where(t => t.Currency == Currency.USD).Sum(t => t.Amount);
        var tjs = totals.Where(t => t.Currency == Currency.TJS).Sum(t => t.Amount);
        decimal? inTjs = usd == 0 ? tjs : usdRate is { } rate ? Math.Round(tjs + usd * rate, 2) : null;
        return new MoneySummary(Ordered(totals), inTjs);
    }

    private static List<MoneyTotal> Ordered(IEnumerable<MoneyTotal> totals) =>
        totals.Where(t => t.Amount != 0).OrderBy(t => t.Currency).ToList();

    private static DateTime UtcStart(DateOnly localDate) => localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - DushanbeTime.Offset;

    private static bool IsTajik => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";

    private DateOnly Today => DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
}
