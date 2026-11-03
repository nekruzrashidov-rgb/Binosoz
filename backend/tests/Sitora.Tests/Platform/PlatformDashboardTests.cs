using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Дашборд платформы: итоги совпадают с базой, доход — только оплаченные тарифы, график за 12 месяцев.
public class PlatformDashboardTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Totals_MatchTheDatabase()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        await TestScenarios.CreateDealAsync(factory);

        var dashboard = await platform.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard", TestJson.Options);

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(await db.Organizations.CountAsync(), dashboard!.TotalOrganizations);
            Assert.Equal(await db.Organizations.CountAsync(o => o.IsActive), dashboard.ActiveOrganizations);
            Assert.Equal(await db.Buildings.CountAsync(), dashboard.TotalBuildings);
            Assert.Equal(await db.Deals.CountAsync(d => d.Stage != DealStage.Cancelled), dashboard.TotalDeals);
            Assert.Equal(await db.Payments.SumAsync(p => p.AmountTjs), dashboard.TotalPaymentsSum);
            Assert.Equal(await db.Customers.CountAsync(), dashboard.TotalCustomers);
            Assert.Equal(await db.Users.CountAsync(u => u.OrganizationId != null), dashboard.TotalStaff);
        });
        Assert.True(dashboard!.NewDealsMonth >= 1);
        Assert.InRange(dashboard.TopOrganizations.Count, 1, 5);
        Assert.Equal(dashboard.TopOrganizations.OrderByDescending(o => o.DealsCount).Select(o => o.DealsCount), dashboard.TopOrganizations.Select(o => o.DealsCount));
    }

    [Fact]
    public async Task DealsByMonth_Has12Months_EndingThisMonth()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var before = (await platform.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard", TestJson.Options))!;
        await TestScenarios.CreateDealAsync(factory);

        var after = (await platform.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard", TestJson.Options))!;

        Assert.Equal(12, after.DealsByMonth.Count);
        var today = DushanbeTime.Today(DateTime.UtcNow);
        Assert.Equal($"{today.Year:D4}-{today.Month:D2}", after.DealsByMonth[^1].Month);
        Assert.Equal(before.DealsByMonth[^1].Count + 1, after.DealsByMonth[^1].Count);
        Assert.True(after.DealsByMonth[^1].Sum > before.DealsByMonth[^1].Sum);
        Assert.Equal(after.DealsByMonth.Select(m => m.Month).Order(), after.DealsByMonth.Select(m => m.Month));
    }

    [Fact]
    public async Task Revenue_CountsOnlyPaidTariffs_NewOrganizationsCounted()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var start = (await platform.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard", TestJson.Options))!;
        var plans = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!.ToDictionary(p => p.Name);

        var paid = await NewOrganizationAsync(platform);
        var unpaid = await NewOrganizationAsync(platform);
        (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{paid}/tariff", new AssignTariffRequest(plans["Basic"].Id, null, true), TestJson.Options)).EnsureSuccessStatusCode();
        (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{unpaid}/tariff", new AssignTariffRequest(plans["Pro"].Id, null, false), TestJson.Options)).EnsureSuccessStatusCode();

        var after = (await platform.GetFromJsonAsync<PlatformDashboardResponse>("/api/v1/platform/dashboard", TestJson.Options))!;
        Assert.Equal(start.PlatformRevenueMonth + 500m, after.PlatformRevenueMonth);
        Assert.Equal(start.NewOrganizationsMonth + 2, after.NewOrganizationsMonth);
        Assert.Equal(start.TotalOrganizations + 2, after.TotalOrganizations);
    }

    [Fact]
    public async Task OnlySuperAdmin()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateAdminClientAsync()).GetAsync("/api/v1/platform/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/platform/dashboard")).StatusCode);
    }

    private static async Task<Guid> NewOrganizationAsync(HttpClient platform)
    {
        var slug = "d-" + Guid.NewGuid().ToString("N")[..10];
        return (await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Дашборд " + slug, slug, $"owner-{slug}@dash.test", "Владелец Дашборда")))).Id;
    }
}
