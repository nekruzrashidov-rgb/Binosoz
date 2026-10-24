using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Payments;

public class CabinetAccessTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Customer_SeesOnlyOwnDeals_AndForeignDealGives404()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, mine.DealId);
        await TestScenarios.CreatePlanAsync(factory, foreign.DealId);
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);

        var deals = await customer.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options);
        var deal = Assert.Single(deals!);
        Assert.Equal(mine.DealId, deal.Id);

        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/deals/{mine.DealId}/schedule")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/deals/{mine.DealId}/documents")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/v1/deals/{mine.DealId}/statement.pdf")).StatusCode);

        // Чужая сделка неотличима от несуществующей.
        foreach (var dealId in new[] { foreign.DealId, Guid.NewGuid() })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/deals/{dealId}/schedule")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/deals/{dealId}/documents")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/v1/deals/{dealId}/statement.pdf")).StatusCode);
        }
    }

    [Fact]
    public async Task Customer_CannotUseStaffEndpoints()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, mine.DealId);
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);

        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/deals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/v1/deals/{mine.DealId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/crm/pipeline")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await TestScenarios.PayAsync(customer, mine.DealId, 1000m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync($"/api/v1/deals/{mine.DealId}/plan",
            new CreatePlanRequest(10, 12, 5, TestScenarios.Today))).StatusCode);
    }

    [Fact]
    public async Task StaffRoles_AccessToSchedule()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var url = $"/api/v1/deals/{deal.DealId}/schedule";

        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateAccountantClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateManagerClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await factory.CreateAdminClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateContentClientAsync()).GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(url)).StatusCode);
    }
}
