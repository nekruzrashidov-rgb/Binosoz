using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

public class ReportTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly DateOnly Today = TestScenarios.Today;

    [Fact]
    public async Task DebtsReport_MatchesSchedule_AndIgnoresLaterPaymentsForPastDate()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var plan = await TestScenarios.CreatePlanAsync(factory, deal.DealId, downPct: 20, months: 6, day: 5, start: Today.AddMonths(-3));
        var accountant = await factory.CreateAccountantClientAsync();
        var down = plan.Items[0];
        var firstMonth = plan.Items[1];
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, down.AmountDue, paidAt: down.DueDate));

        // На сегодня — те же цифры, что в графике.
        var schedule = await accountant.GetFromJsonAsync<ScheduleResponse>($"/api/v1/deals/{deal.DealId}/schedule", TestJson.Options);
        var today = await Debts(accountant, null);
        var row = Assert.Single(today.Rows, r => r.DealId == deal.DealId);
        Assert.Equal(schedule!.Overdue, row.Overdue);
        Assert.Equal(firstMonth.DueDate, row.OldestOverdueDate);
        Assert.Equal(Today.DayNumber - firstMonth.DueDate.DayNumber, row.DaysOverdue);
        Assert.Equal(schedule.Remaining, row.Remaining);
        Assert.Equal(Currency.TJS, row.Currency);
        Assert.Equal(today.Rows.Where(r => r.Currency == Currency.TJS).Sum(r => r.Overdue),
            Assert.Single(today.TotalsByCurrency, t => t.Currency == Currency.TJS).Amount);

        // В день первого месячного платежа просрочки ещё нет, на следующий день — есть.
        Assert.DoesNotContain((await Debts(accountant, firstMonth.DueDate)).Rows, r => r.DealId == deal.DealId);
        var nextDay = firstMonth.DueDate.AddDays(1);
        Assert.Equal(firstMonth.AmountDue, Assert.Single((await Debts(accountant, nextDay)).Rows, r => r.DealId == deal.DealId).Overdue);

        // Оплата сегодня закрывает долг на сегодня, но не меняет картину на прошлую дату.
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, firstMonth.AmountDue));
        Assert.Equal(firstMonth.AmountDue, Assert.Single((await Debts(accountant, nextDay)).Rows, r => r.DealId == deal.DealId).Overdue);
        var afterPayment = Assert.Single((await Debts(accountant, null)).Rows, r => r.DealId == deal.DealId);
        Assert.Equal(plan.Items[2].DueDate, afterPayment.OldestOverdueDate);
    }

    [Fact]
    public async Task PaymentsReport_FiltersByPeriod_IncludesReversals_AndTotalsAddUp()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: Today.AddMonths(-1));
        var accountant = await factory.CreateAccountantClientAsync();
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 1000m, paidAt: Today.AddDays(-10)));
        var mistaken = await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 2000m));
        await TestScenarios.ReadAsync<PaymentResponse>(await accountant.PostAsJsonAsync(
            $"/api/v1/payments/{mistaken.Id}/reverse", new ReversePaymentRequest("Ошибка в сумме")));

        var wide = await Payments(accountant, Today.AddDays(-10), Today);
        var mine = wide.Rows.Where(r => r.DealId == deal.DealId).ToList();
        Assert.Equal([1000m, 2000m, -2000m], mine.Select(r => r.Amount));
        Assert.Equal([false, false, true], mine.Select(r => r.IsReversal));
        Assert.Equal("Покупатель Кабинета", mine[0].CustomerName);
        Assert.Equal(wide.Rows.Where(r => r.Currency == Currency.TJS).Sum(r => r.Amount),
            Assert.Single(wide.TotalsByCurrency, t => t.Currency == Currency.TJS).Amount);
        Assert.Equal(wide.Rows.Sum(r => r.AmountTjs), wide.TotalTjs);

        var narrow = await Payments(accountant, Today.AddDays(-5), Today);
        Assert.Equal([2000m, -2000m], narrow.Rows.Where(r => r.DealId == deal.DealId).Select(r => r.Amount));

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await accountant.GetAsync($"/api/v1/reports/payments?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await accountant.GetAsync($"/api/v1/reports/payments?from={Today.AddYears(-2):yyyy-MM-dd}&to={Today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task ExcelExports_OpenAndContainReportRows()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: Today.AddMonths(-2));
        var accountant = await factory.CreateAccountantClientAsync();
        await TestScenarios.ReadAsync<PaymentResponse>(await TestScenarios.PayAsync(accountant, deal.DealId, 1234.5m));

        var payments = await accountant.GetAsync($"/api/v1/reports/payments.xlsx?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", payments.Content.Headers.ContentType?.MediaType);
        using (var workbook = new XLWorkbook(new MemoryStream(await payments.Content.ReadAsByteArrayAsync())))
        {
            var sheet = workbook.Worksheet("Поступления");
            Assert.Contains(sheet.RowsUsed(), r => r.Cell(2).GetString() == "Покупатель Кабинета" && r.Cell(5).GetValue<decimal>() == 1234.5m);
            Assert.Contains(sheet.RowsUsed(), r => r.Cell(1).GetString() == "Итого в сомони");
        }

        var debts = await accountant.GetAsync("/api/v1/reports/debts.xlsx");
        using (var workbook = new XLWorkbook(new MemoryStream(await debts.Content.ReadAsByteArrayAsync())))
        {
            var sheet = workbook.Worksheet("Задолженность");
            Assert.Contains(sheet.RowsUsed(), r => r.Cell(2).GetString() == deal.Phone);
        }
    }

    [Fact]
    public async Task Reports_OnlyForAccountantAndAdmin()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        const string url = "/api/v1/reports/debts";

        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateAccountantClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateAdminClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateContentClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateCustomerClientAsync(deal.Phone)).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
    }

    private static async Task<DebtsReportResponse> Debts(HttpClient client, DateOnly? asOf) =>
        (await client.GetFromJsonAsync<DebtsReportResponse>(
            "/api/v1/reports/debts" + (asOf is { } d ? $"?asOf={d:yyyy-MM-dd}" : ""), TestJson.Options))!;

    private static async Task<PaymentsReportResponse> Payments(HttpClient client, DateOnly from, DateOnly to) =>
        (await client.GetFromJsonAsync<PaymentsReportResponse>(
            $"/api/v1/reports/payments?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", TestJson.Options))!;
}
