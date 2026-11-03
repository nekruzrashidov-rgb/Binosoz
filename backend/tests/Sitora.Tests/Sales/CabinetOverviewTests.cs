using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Reviews;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Sales;

// Первый экран кабинета (GET /cabinet/overview) и «печатает...» в чате сделки.
public class CabinetOverviewTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string Url = "/api/v1/cabinet/overview";

    [Fact]
    public async Task NoDeals_HasDealsFalse()
    {
        // Только что зарегистрировался на сайте: карточка клиента есть, сделок нет.
        var email = $"new{Guid.NewGuid():N}@customers.test";
        (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new RegisterCustomerRequest(email, "Некруз", "Рашидов", SitoraApiFactory.RandomPhone(), Organization.DefaultSlug))).EnsureSuccessStatusCode();
        var client = await factory.CreateEmailClientAsync(email);

        var overview = await TestScenarios.ReadAsync<CabinetOverviewResponse>(await client.GetAsync(Url));

        Assert.False(overview.HasDeals);
        Assert.Contains("Некруз", overview.CustomerName);
        Assert.Empty(overview.Deals);
        Assert.NotNull(overview.Contacts);
    }

    [Fact]
    public async Task Deal_ShowsApartmentMoneyManager_AndPaymentsMinusReversals()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        await TestScenarios.CreatePlanAsync(factory, deal.DealId);
        var accountant = await factory.CreateAccountantClientAsync();
        await TestScenarios.ReadAsync<JsonElement>(await TestScenarios.PayAsync(accountant, deal.DealId, 1000m));
        var wrong = await TestScenarios.ReadAsync<JsonElement>(await TestScenarios.PayAsync(accountant, deal.DealId, 500m));
        (await accountant.PostAsJsonAsync($"/api/v1/payments/{wrong.GetProperty("id").GetGuid()}/reverse", new { reason = "Ошибка суммы" })).EnsureSuccessStatusCode();
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);

        var overview = await TestScenarios.ReadAsync<CabinetOverviewResponse>(await customer.GetAsync(Url));

        Assert.True(overview.HasDeals);
        Assert.False(string.IsNullOrWhiteSpace(overview.CustomerName));
        var d = Assert.Single(overview.Deals);
        Assert.Equal(deal.DealId, d.DealId);
        Assert.NotEqual(Guid.Empty, d.BuildingId);
        Assert.False(string.IsNullOrWhiteSpace(d.BuildingName));
        Assert.True(d.Area > 0);
        Assert.False(string.IsNullOrWhiteSpace(d.ManagerName));
        Assert.Equal(1000m, d.Paid);
        Assert.Equal(d.FinalPrice - 1000m, d.Remaining);
        Assert.Equal((int)Math.Floor(1000m * 100 / d.FinalPrice), d.PaidPercent);
        Assert.Null(d.MyReview);
    }

    [Fact]
    public async Task OwnReview_IsShown_OtherCustomersDealsAreNot()
    {
        var mine = await TestScenarios.CreateDealAsync(factory);
        var other = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(mine.Phone);
        var first = await TestScenarios.ReadAsync<CabinetOverviewResponse>(await customer.GetAsync(Url));
        var buildingId = Assert.Single(first.Deals).BuildingId;

        (await customer.PostAsJsonAsync($"/api/v1/buildings/{buildingId}/reviews", new SaveReviewRequest(4, "Хороший дом, стройка идёт по графику"))).EnsureSuccessStatusCode();
        var overview = await TestScenarios.ReadAsync<CabinetOverviewResponse>(await customer.GetAsync(Url));

        var d = Assert.Single(overview.Deals);
        Assert.DoesNotContain(overview.Deals, x => x.DealId == other.DealId);
        Assert.NotNull(d.MyReview);
        Assert.Equal((4, "Хороший дом, стройка идёт по графику", false), (d.MyReview!.Rating, d.MyReview.Text, d.MyReview.IsPublished == true));
    }

    [Fact]
    public async Task OnlyCustomers()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Typing_ReachesTheOtherSide_ForeignDealIsRefused()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var foreign = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var manager = await factory.CreateManagerClientAsync();
        await using var customerHub = Connect(Token(customer));
        await using var managerHub = Connect(Token(manager));
        var managerSaw = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var customerSawOwn = false;
        managerHub.On<JsonElement>("typing", e => managerSaw.TrySetResult(e));
        customerHub.On<JsonElement>("typing", _ => customerSawOwn = true);
        await customerHub.StartAsync();
        await managerHub.StartAsync();
        await customerHub.InvokeAsync("JoinDeal", deal.DealId);
        await managerHub.InvokeAsync("JoinDeal", deal.DealId);

        await customerHub.InvokeAsync("SendTyping", deal.DealId);

        var e = await managerSaw.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(e.GetProperty("fromCustomer").GetBoolean());
        Assert.Equal(deal.DealId, e.GetProperty("dealId").GetGuid());
        Assert.False(customerSawOwn);
        await Assert.ThrowsAsync<HubException>(() => customerHub.InvokeAsync("SendTyping", foreign.DealId));
    }

    private HubConnection Connect(string token)
    {
        var server = factory.Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, "/hubs/chat"), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
    }

    private static string Token(HttpClient client) => client.DefaultRequestHeaders.Authorization!.Parameter!;
}
