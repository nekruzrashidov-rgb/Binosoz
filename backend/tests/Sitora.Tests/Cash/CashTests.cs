using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Cash;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;
using Sitora.Tests.Payments;

namespace Sitora.Tests.Cash;

public class CashTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static DateOnly Today => TestScenarios.Today;

    [Fact]
    public async Task AddIncome_IsPositive_AndWritesAudit()
    {
        var (accountant, building, _) = await SetupAsync();

        var tx = await AddAsync(accountant, building, 1500m, CashDirection.Income, CashCategory.Rent, Today);

        Assert.Equal((1500m, 1500m, CashDirection.Income, false), (tx.Amount, tx.AmountTjs, tx.Direction, tx.IsReversal));
        Assert.False(string.IsNullOrWhiteSpace(tx.CreatedByName));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Entity == "CashTransaction" && l.EntityId == tx.Id && l.Action == "Created")));
    }

    [Fact]
    public async Task AddExpense_IsNegative_AndChecksCategoryDateAndDeal()
    {
        var (accountant, building, dealId) = await SetupAsync();

        var tx = await AddAsync(accountant, building, 300m, CashDirection.Expense, CashCategory.Materials, Today, dealId);
        Assert.Equal((-300m, CashDirection.Expense, dealId), (tx.Amount, tx.Direction, tx.DealId));

        // Расходная статья у прихода, дата в будущем, сделка не из этого дома — 422.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(accountant, building, 10m, CashDirection.Income, CashCategory.Tax, Today)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(accountant, building, 10m, CashDirection.Expense, CashCategory.Tax, Today.AddDays(1))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await PostAsync(accountant, building, 10m, CashDirection.Expense, CashCategory.Tax, Today, Guid.NewGuid())).StatusCode);
        // Отрицательная сумма — ошибка валидации (400): знак ставит направление.
        Assert.Equal(HttpStatusCode.BadRequest, (await PostAsync(accountant, building, -10m, CashDirection.Expense, CashCategory.Tax, Today)).StatusCode);
    }

    [Fact]
    public async Task Reverse_CreatesOppositeEntry_OnlyOnce()
    {
        var (accountant, building, _) = await SetupAsync();
        var original = await AddAsync(accountant, building, 800m, CashDirection.Income, CashCategory.OtherIncome, Today.AddDays(-3));

        var reversal = await TestScenarios.ReadAsync<CashTransactionResponse>(await ReverseAsync(accountant, building, original.Id));

        Assert.Equal((true, original.Id, -800m, -800m, CashDirection.Income, Today), (reversal.IsReversal, reversal.ReversalOfId, reversal.Amount, reversal.AmountTjs, reversal.Direction, reversal.OperationDate));
        Assert.Equal("Ошибка в сумме", reversal.Description);
        var book = await BookAsync(accountant, building, Today.AddDays(-3), Today);
        Assert.True(book.Transactions.Single(t => t.Id == original.Id).IsReversed);

        // Второе сторно и сторно сторно — 422; чужой id — 404.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ReverseAsync(accountant, building, original.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await ReverseAsync(accountant, building, reversal.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ReverseAsync(accountant, building, Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task Book_SumsInSomoni_UsdByRateOfTheDay()
    {
        var (accountant, building, _) = await SetupAsync();
        var day = Today.AddDays(-40);
        await ExchangeRateTests.SetRateAsync(accountant, day, 10.5m);

        await AddAsync(accountant, building, 1000m, CashDirection.Income, CashCategory.Rent, day);
        var usd = await AddAsync(accountant, building, 100m, CashDirection.Income, CashCategory.OtherIncome, day, currency: Currency.USD);
        await AddAsync(accountant, building, 300m, CashDirection.Expense, CashCategory.Utilities, day);
        Assert.Equal((100m, 1050m), (usd.Amount, usd.AmountTjs));

        var book = await BookAsync(accountant, building, day, day);

        Assert.Equal((2050m, 300m, 1750m, 3), (book.TotalIncomeTjs, book.TotalExpenseTjs, book.BalanceTjs, book.Transactions.Count));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await accountant.GetAsync($"/api/v1/buildings/{building}/cash?from={Today:yyyy-MM-dd}&to={day:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task Excel_HasBookSheetWithTotals_AndReceiptIsPdf()
    {
        var (accountant, building, dealId) = await SetupAsync();
        var day = Today.AddDays(-60);
        var income = await AddAsync(accountant, building, 2000m, CashDirection.Income, CashCategory.ApartmentPayment, day, dealId);
        await AddAsync(accountant, building, 500m, CashDirection.Expense, CashCategory.Salaries, day);

        var response = await accountant.GetAsync($"/api/v1/buildings/{building}/cash/export.xlsx?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheet("Кассовая книга");
        Assert.Contains(sheet.Column(8).CellsUsed(), c => c.GetString().StartsWith("Покупатель Кабинета, кв. №"));
        var balance = sheet.Column(4).CellsUsed().Single(c => c.GetString() == "Баланс, TJS");
        Assert.Equal(1500m, sheet.Cell(balance.Address.RowNumber, 7).GetValue<decimal>());

        var pdf = await accountant.GetAsync($"/api/v1/buildings/{building}/cash/{income.Id}/receipt.pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);
        var reversal = await TestScenarios.ReadAsync<CashTransactionResponse>(await ReverseAsync(accountant, building, income.Id));
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync($"/api/v1/buildings/{building}/cash/{reversal.Id}/receipt.pdf")).StatusCode);
        Assert.Equal(income.Id.ToString("N")[^8..].ToUpperInvariant(), CashService.Number(income.Id));
    }

    [Fact]
    public async Task Roles_AccountantAndLeaders_OthersForbidden_UnknownBuilding404()
    {
        var (accountant, building, _) = await SetupAsync();
        var owner = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/v1/buildings/{building}/cash")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(owner, building, 50m, CashDirection.Expense, CashCategory.Marketing, Today)).StatusCode);

        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync($"/api/v1/buildings/{building}/cash")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(manager, building, 50m, CashDirection.Expense, CashCategory.Marketing, Today)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/buildings/{building}/cash")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await accountant.GetAsync($"/api/v1/buildings/{Guid.NewGuid()}/cash")).StatusCode);
    }

    private async Task<(HttpClient Accountant, Guid BuildingId, Guid DealId)> SetupAsync()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var building = await factory.WithDbAsync(db => db.Deals.Where(d => d.Id == deal.DealId).Select(d => d.Apartment.Floor.BuildingId).SingleAsync());
        return (await factory.CreateAccountantClientAsync(), building, deal.DealId);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, Guid building, decimal amount, CashDirection direction,
        CashCategory category, DateOnly date, Guid? dealId = null, Currency currency = Currency.TJS) =>
        client.PostAsJsonAsync($"/api/v1/buildings/{building}/cash",
            new CreateCashTransactionRequest(amount, currency, direction, category, "Тестовая операция", date, dealId), TestJson.Options);

    private static async Task<CashTransactionResponse> AddAsync(HttpClient client, Guid building, decimal amount, CashDirection direction,
        CashCategory category, DateOnly date, Guid? dealId = null, Currency currency = Currency.TJS) =>
        await TestScenarios.ReadAsync<CashTransactionResponse>(await PostAsync(client, building, amount, direction, category, date, dealId, currency));

    private static Task<HttpResponseMessage> ReverseAsync(HttpClient client, Guid building, Guid id) =>
        client.PostAsJsonAsync($"/api/v1/buildings/{building}/cash/{id}/reverse", new ReverseCashTransactionRequest("Ошибка в сумме"), TestJson.Options);

    private static async Task<CashBookResponse> BookAsync(HttpClient client, Guid building, DateOnly from, DateOnly to) =>
        (await client.GetFromJsonAsync<CashBookResponse>($"/api/v1/buildings/{building}/cash?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", TestJson.Options))!;
}
