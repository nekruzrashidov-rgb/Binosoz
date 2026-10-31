using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Sitora.Domain.DTOs.Reports;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Reports;

// Статистика CRM. Тесты класса делят одну базу, поэтому проверяют изменения («было → стало»), а не абсолютные числа.
public class StatsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [Fact]
    public async Task Dashboard_ReflectsNewDealAndBooking_RightAway()
    {
        var owner = await factory.CreateAdminClientAsync();
        var before = await Dashboard(owner);

        var deal = await TestScenarios.CreateDealAsync(factory);
        var manager = await factory.CreateManagerClientAsync();
        (await manager.PostAsJsonAsync($"/api/v1/apartments/{await factory.FindFreeApartmentAsync()}/bookings",
            new CreateBookingRequest("Гость статистики", SitoraApiFactory.RandomPhone(), null, null))).EnsureSuccessStatusCode();

        // Кэш на 5 минут сбрасывается сделкой и бронью — цифры обновляются сразу.
        var after = await Dashboard(owner);
        Assert.Equal(before.TotalApartments, after.TotalApartments);
        Assert.Equal(before.Free - 2, after.Free);
        Assert.Equal(before.Booked + 1, after.Booked);
        Assert.Equal(before.Sold + 1, after.Sold);
        Assert.Equal(before.NewDealsThisMonth + 1, after.NewDealsThisMonth);
        Assert.Equal(TjsOf(before.TotalRevenue) + deal.Price, TjsOf(after.TotalRevenue));
        Assert.Equal(TjsOf(before.Remaining) + deal.Price, TjsOf(after.Remaining));
    }

    [Fact]
    public async Task Payments_AddToCollected_AndSalesGroupByMonth()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var owner = await factory.CreateAdminClientAsync();
        var accountant = await factory.CreateAccountantClientAsync();
        var today = TestScenarios.Today;
        var lastMonth = today.AddMonths(-1);
        var beforeDashboard = await Dashboard(owner);
        var beforeSales = await Sales(owner);

        (await TestScenarios.PayAsync(accountant, deal.DealId, 1000m, paidAt: lastMonth)).EnsureSuccessStatusCode();
        (await TestScenarios.PayAsync(accountant, deal.DealId, 2500m, paidAt: today)).EnsureSuccessStatusCode();

        var dashboard = await Dashboard(owner);
        Assert.Equal(beforeDashboard.CollectedRevenue.Tjs + 3500m, dashboard.CollectedRevenue.Tjs);
        Assert.Equal(TjsOf(beforeDashboard.Remaining) - 3500m, TjsOf(dashboard.Remaining));

        var sales = await Sales(owner);
        Assert.Equal(6, sales.Count);
        Assert.Equal(today.ToString("yyyy-MM"), sales[^1].Month);
        Assert.Equal(lastMonth.ToString("yyyy-MM"), sales[^2].Month);
        Assert.Equal(beforeSales[^1].Revenue.Tjs + 2500m, sales[^1].Revenue.Tjs);
        Assert.Equal(beforeSales[^2].Revenue.Tjs + 1000m, sales[^2].Revenue.Tjs);

        Assert.Equal(HttpStatusCode.UnprocessableEntity,
            (await owner.GetAsync($"/api/v1/stats/sales?from={today.AddMonths(-30):yyyy-MM-dd}&to={today:yyyy-MM-dd}")).StatusCode);
    }

    [Fact]
    public async Task UnpaidScheduleItemsInThePast_AreOverdue()
    {
        var owner = await factory.CreateAdminClientAsync();
        var before = await Dashboard(owner);

        // График начат два месяца назад и не оплачен: прошедшие сроки — просрочка.
        var deal = await TestScenarios.CreateDealAsync(factory);
        var schedule = await TestScenarios.CreatePlanAsync(factory, deal.DealId, start: TestScenarios.Today.AddMonths(-2));
        var due = schedule.Items.Count(i => i.DueDate < TestScenarios.Today);
        Assert.True(due > 0);

        var after = await Dashboard(owner);
        Assert.Equal(before.OverduePayments + due, after.OverduePayments);
        Assert.Equal(before.OverdueDeals + 1, after.OverdueDeals);
    }

    [Fact]
    public async Task Buildings_ForLeaders_WithOccupancy()
    {
        var owner = await factory.CreateAdminClientAsync();

        var rows = await owner.GetFromJsonAsync<List<BuildingStatsRow>>("/api/v1/stats/buildings", TestJson.Options);

        var building = Assert.Single(rows!, r => r.TotalApartments > 0);
        Assert.Equal(building.TotalApartments, building.Free + building.Booked + building.Sold + building.OnRequest);
        Assert.Equal(Math.Round((decimal)(building.Booked + building.Sold) / building.TotalApartments, 4), building.OccupancyRate);
    }

    [Fact]
    public async Task Export_IsXlsxWithFourSheets_ForOwnerAndAccountant()
    {
        await TestScenarios.CreateDealAsync(factory);
        foreach (var client in new[] { await factory.CreateAccountantClientAsync(), await factory.CreateAdminClientAsync() })
        {
            var response = await client.GetAsync("/api/v1/stats/export");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Xlsx, response.Content.Headers.ContentType?.MediaType);
            using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
            Assert.Equal(["Сделки", "График платежей", "Поступления", "Сводка"], workbook.Worksheets.Select(w => w.Name));
            Assert.True(workbook.Worksheet("Сделки").LastRowUsed()!.RowNumber() >= 4);
        }
    }

    [Fact]
    public async Task Roles_ManagerSeesDashboardOnly_AccountantExportsButCannotSell_OthersForbidden()
    {
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/stats/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/stats/sales")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/stats/buildings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/stats/export")).StatusCode);

        var accountant = await factory.CreateAccountantClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/v1/stats/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/v1/stats/buildings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/v1/stats/export")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PostAsJsonAsync("/api/v1/deals",
            new CreateDealRequest(await factory.FindFreeApartmentAsync(), Guid.NewGuid(), DealStage.Contract, Currency.TJS, null, null), TestJson.Options)).StatusCode);

        var deal = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/stats/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreatePlatformOwnerClientAsync()).GetAsync("/api/v1/stats/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/stats/dashboard")).StatusCode);
    }

    private static async Task<DashboardStatsResponse> Dashboard(HttpClient client) =>
        (await client.GetFromJsonAsync<DashboardStatsResponse>("/api/v1/stats/dashboard", TestJson.Options))!;

    private static async Task<List<MonthlySalesRow>> Sales(HttpClient client) =>
        (await client.GetFromJsonAsync<List<MonthlySalesRow>>("/api/v1/stats/sales", TestJson.Options))!;

    private static decimal TjsOf(MoneySummary money) => money.ByCurrency.Where(m => m.Currency == Currency.TJS).Sum(m => m.Amount);
}
