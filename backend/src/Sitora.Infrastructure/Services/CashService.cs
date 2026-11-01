using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Кассовая книга дома. Какие дома видны, решают фильтры организации и домов в AppDbContext:
// чужой дом и его операции — 404. Операции не меняются и не удаляются, только сторнируются.
public class CashService(
    AppDbContext db,
    IExchangeRateService rates,
    ICurrentUser currentUser,
    IAuditService audit,
    IPdfGenerator pdf,
    TimeProvider timeProvider) : ICashService
{
    private const string BuildingNotFound = "Дом не найден";
    private const string TransactionNotFound = "Операция не найдена";
    private const string MoneyFormat = "#,##0.00";

    public async Task<CashTransactionResponse> AddAsync(Guid buildingId, CreateCashTransactionRequest request, CancellationToken ct = default)
    {
        var building = await FindBuildingAsync(buildingId, ct);
        if (CashCategories.DirectionOf(request.Category) != request.Direction)
            throw new BusinessRuleException(request.Direction == CashDirection.Income
                ? "Эта статья — расходная, а операция — приход"
                : "Эта статья — приходная, а операция — расход");
        if (request.OperationDate > Today)
            throw new BusinessRuleException("Дата операции не может быть в будущем");
        if (request.DealId is { } dealId
            && !await db.Deals.AnyAsync(d => d.Id == dealId && d.Apartment.Floor.BuildingId == buildingId, ct))
            throw new BusinessRuleException("Сделка не найдена в этом доме");

        decimal? rate = null;
        if (request.Currency != Currency.TJS)
            rate = await rates.GetRateOnAsync(request.Currency, request.OperationDate, building.OrganizationId, ct)
                ?? throw new BusinessRuleException($"Нет курса {request.Currency} на {request.OperationDate:dd.MM.yyyy}. Сначала введите курс");

        var sign = request.Direction == CashDirection.Income ? 1 : -1;
        var amount = sign * request.Amount;
        var tx = new CashTransaction
        {
            BuildingId = buildingId,
            Amount = amount,
            Currency = request.Currency,
            RateUsed = rate,
            AmountTjs = rate is { } r ? Math.Round(amount * r, 2, MidpointRounding.AwayFromZero) : amount,
            Direction = request.Direction,
            Category = request.Category,
            Description = request.Description.Trim(),
            DealId = request.DealId,
            OperationDate = request.OperationDate,
            CreatedById = UserId
        };
        db.CashTransactions.Add(tx);
        audit.Record(nameof(CashTransaction), tx.Id, "Created", null, Snapshot(tx), building.OrganizationId);
        await db.SaveChangesAsync(ct);

        return await GetAsync(tx.Id, ct);
    }

    public async Task<CashTransactionResponse> ReverseAsync(Guid buildingId, Guid transactionId, string reason, CancellationToken ct = default)
    {
        var building = await FindBuildingAsync(buildingId, ct);
        var original = await db.CashTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.BuildingId == buildingId, ct)
            ?? throw new NotFoundException(TransactionNotFound);
        if (original.IsReversal)
            throw new BusinessRuleException("Сторно нельзя сторнировать");
        if (await db.CashTransactions.AnyAsync(t => t.ReversalOfId == transactionId, ct))
            throw new BusinessRuleException("Операция уже сторнирована");

        // Сторно повторяет операцию с обратным знаком и тем же курсом; дата — день исправления.
        var reversal = new CashTransaction
        {
            BuildingId = buildingId,
            Amount = -original.Amount,
            Currency = original.Currency,
            RateUsed = original.RateUsed,
            AmountTjs = -original.AmountTjs,
            Direction = original.Direction,
            Category = original.Category,
            Description = reason.Trim(),
            DealId = original.DealId,
            ReversalOfId = original.Id,
            OperationDate = Today,
            CreatedById = UserId
        };
        db.CashTransactions.Add(reversal);
        audit.Record(nameof(CashTransaction), original.Id, "Reversed", Snapshot(original), Snapshot(reversal), building.OrganizationId);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            // Два сторно одновременно: второе упирается в уникальный индекс.
            throw new BusinessRuleException("Операция уже сторнирована");
        }

        return await GetAsync(reversal.Id, ct);
    }

    public async Task<CashBookResponse> GetBookAsync(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var building = await FindBuildingAsync(buildingId, ct);
        var (start, end) = Period(from, to);

        var transactions = await Project(db.CashTransactions
                .Where(t => t.BuildingId == buildingId && t.OperationDate >= start && t.OperationDate <= end)
                .OrderByDescending(t => t.OperationDate).ThenByDescending(t => t.CreatedAt))
            .ToListAsync(ct);

        var income = transactions.Where(t => t.Direction == CashDirection.Income).Sum(t => t.AmountTjs);
        var expense = -transactions.Where(t => t.Direction == CashDirection.Expense).Sum(t => t.AmountTjs);
        return new CashBookResponse(buildingId, building.NameRu, start, end, income, expense, income - expense, transactions);
    }

    public async Task<byte[]> GetBookExcelAsync(Guid buildingId, DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var book = await GetBookAsync(buildingId, from, to, ct);
        // Числа сделок — для колонки «Сделка»: покупатель и квартира понятнее идентификатора.
        var dealIds = book.Transactions.Where(t => t.DealId is not null).Select(t => t.DealId!.Value).Distinct().ToList();
        var deals = await DealTextsAsync(dealIds, ct);

        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Кассовая книга");
        sheet.Cell(1, 1).Value = $"Кассовая книга: {book.BuildingName}";
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(2, 1).Value = $"Период: {book.From:dd.MM.yyyy} — {book.To:dd.MM.yyyy}";

        string[] headers = ["Дата", "Направление", "Категория", "Описание", "Сумма", "Валюта", "В сомони", "Сделка", "Кто внёс", "Сторно"];
        for (var c = 0; c < headers.Length; c++)
            sheet.Cell(4, c + 1).Value = headers[c];
        sheet.Row(4).Style.Font.Bold = true;

        var r = 5;
        // В книге — по порядку дат, как в бумажной кассовой книге.
        foreach (var t in book.Transactions.Reverse())
        {
            sheet.Cell(r, 1).Value = t.OperationDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(r, 2).Value = t.Direction == CashDirection.Income ? "Приход" : "Расход";
            sheet.Cell(r, 3).Value = CategoryText(t.Category);
            sheet.Cell(r, 4).Value = t.Description;
            sheet.Cell(r, 5).Value = t.Amount;
            sheet.Cell(r, 6).Value = t.Currency.ToString();
            sheet.Cell(r, 7).Value = t.AmountTjs;
            sheet.Cell(r, 8).Value = t.DealId is { } d && deals.TryGetValue(d, out var text) ? text : "";
            sheet.Cell(r, 9).Value = t.CreatedByName;
            sheet.Cell(r, 10).Value = t.IsReversal ? "сторно" : t.IsReversed ? "сторнирована" : "";
            var color = t.IsReversal || t.IsReversed ? "#F5F5F5" : t.Direction == CashDirection.Income ? "#E8F5E9" : "#FFEBEE";
            sheet.Range(r, 1, r, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml(color);
            r++;
        }

        r++;
        foreach (var (title, value) in new[] { ("Итого приход, TJS", book.TotalIncomeTjs), ("Итого расход, TJS", book.TotalExpenseTjs), ("Баланс, TJS", book.BalanceTjs) })
        {
            sheet.Cell(r, 4).Value = title;
            sheet.Cell(r, 7).Value = value;
            sheet.Row(r).Style.Font.Bold = true;
            r++;
        }

        sheet.Column(1).Style.DateFormat.Format = "dd.MM.yyyy";
        sheet.Column(5).Style.NumberFormat.Format = MoneyFormat;
        sheet.Column(7).Style.NumberFormat.Format = MoneyFormat;
        sheet.Columns().AdjustToContents();
        sheet.Column(4).Width = Math.Min(sheet.Column(4).Width, 60);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> GetReceiptPdfAsync(Guid buildingId, Guid transactionId, CancellationToken ct = default)
    {
        var building = await db.Buildings.Where(b => b.Id == buildingId)
            .Select(b => new { b.NameRu, Organization = b.Organization.Name })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException(BuildingNotFound);
        var tx = await Project(db.CashTransactions.Where(t => t.Id == transactionId && t.BuildingId == buildingId)).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException(TransactionNotFound);

        var dealText = tx.DealId is { } dealId ? (await DealTextsAsync([dealId], ct)).GetValueOrDefault(dealId) : null;
        var data = new CashReceiptData(building.Organization, building.NameRu, Number(tx.Id), tx, CategoryText(tx.Category), dealText,
            tx.ReversalOfId is { } of ? Number(of) : null, timeProvider.GetUtcNow().UtcDateTime);
        return pdf.RenderCashReceipt(data);
    }

    // Номер квитанции — конец идентификатора, как короткий номер в CRM: начало UUID v7 — время, оно у соседних записей совпадает.
    public static string Number(Guid id) => id.ToString("N")[^8..].ToUpperInvariant();

    public static string CategoryText(CashCategory category) => category switch
    {
        CashCategory.ApartmentPayment => "Оплата покупателя",
        CashCategory.Rent => "Аренда",
        CashCategory.OtherIncome => "Прочий приход",
        CashCategory.Construction => "Строительство",
        CashCategory.Materials => "Материалы",
        CashCategory.Salaries => "Зарплата",
        CashCategory.Marketing => "Маркетинг",
        CashCategory.Utilities => "Коммунальные услуги",
        CashCategory.Tax => "Налоги",
        _ => "Прочий расход"
    };

    private IQueryable<CashTransactionResponse> Project(IQueryable<CashTransaction> source) =>
        source.AsNoTracking().Select(t => new CashTransactionResponse(
            t.Id, t.BuildingId, t.Amount, t.Currency, t.AmountTjs, t.Direction, t.Category, t.Description, t.DealId,
            t.ReversalOfId != null, t.ReversalOfId,
            db.CashTransactions.Any(x => x.ReversalOfId == t.Id),
            t.OperationDate, t.CreatedAt, t.CreatedBy.FullName));

    private async Task<CashTransactionResponse> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.CashTransactions.Where(t => t.Id == id)).SingleAsync(ct);

    private async Task<Dictionary<Guid, string>> DealTextsAsync(List<Guid> dealIds, CancellationToken ct) =>
        await db.Deals.Where(d => dealIds.Contains(d.Id))
            .Select(d => new { d.Id, Buyer = d.Customer.FullName, d.Apartment.Number })
            .ToDictionaryAsync(d => d.Id, d => $"{d.Buyer}, кв. №{d.Number}", ct);

    private async Task<Building> FindBuildingAsync(Guid buildingId, CancellationToken ct) =>
        await db.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == buildingId, ct)
            ?? throw new NotFoundException(BuildingNotFound);

    // По умолчанию — текущий месяц; не больше года, как у остальных отчётов.
    private (DateOnly Start, DateOnly End) Period(DateOnly? from, DateOnly? to)
    {
        var today = Today;
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1);
        if (end < start)
            throw new BusinessRuleException("Дата окончания периода раньше даты начала");
        if (end.DayNumber - start.DayNumber >= ReportService.MaxPeriodDays)
            throw new BusinessRuleException($"Период — не больше {ReportService.MaxPeriodDays} дней");
        return (start, end);
    }

    private static object Snapshot(CashTransaction t) => new
    {
        t.Amount,
        Currency = t.Currency.ToString(),
        t.AmountTjs,
        Direction = t.Direction.ToString(),
        Category = t.Category.ToString(),
        t.Description,
        t.DealId,
        t.OperationDate,
        t.ReversalOfId
    };

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");

    private DateOnly Today => DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
}
