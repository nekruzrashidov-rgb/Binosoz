using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sitora.Domain.DTOs.Auth;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Демо-доступ для жюри: аккаунты создаются только при SEED_DEMO_ACCOUNTS=true с паролями из секретов (здесь — тестовые
// значения); вход демо-покупателя паролем — только с Auth:DemoPasswordLogin=true.
public class DemoAccountsFactory : SitoraApiFactory
{
    public const string BuyerPassword = "Demo-Buyer-Test-1";
    public const string DemoManagerPassword = "Demo-Manager-Test-1";
    protected virtual bool PasswordLogin => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("SEED_DEMO_ACCOUNTS", "true");
        builder.UseSetting("SEED_DEMO_BUYER_PASSWORD", BuyerPassword);
        builder.UseSetting("SEED_DEMO_MANAGER_PASSWORD", DemoManagerPassword);
        builder.UseSetting("Auth:DemoPasswordLogin", PasswordLogin ? "true" : "false");
    }
}

public class DemoAccountsNoPasswordLoginFactory : DemoAccountsFactory
{
    protected override bool PasswordLogin => false;
}

public class DemoAccessTests(DemoAccountsFactory factory) : IClassFixture<DemoAccountsFactory>
{
    [Fact]
    public async Task DemoBuyer_SignsInWithPassword_SeesOneDealWithScheduleChatAndIssuedPdf()
    {
        var methods = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.True(methods.GetProperty("demo").GetBoolean());

        var buyer = await factory.CreateClientForAsync(DemoAccountsSeeder.BuyerEmail, DemoAccountsFactory.BuyerPassword);
        var deals = await buyer.GetFromJsonAsync<JsonElement>("/api/v1/me/deals");
        var deal = Assert.Single(deals.EnumerateArray());
        var dealId = deal.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await buyer.GetAsync($"/api/v1/deals/{dealId}/schedule")).StatusCode);
        var messages = await buyer.GetFromJsonAsync<JsonElement>($"/api/v1/deals/{dealId}/messages");
        Assert.True(messages.GetProperty("items").GetArrayLength() >= 2);

        // Акт сверки сформирован настоящим генератором и учтён с числом страниц из файла.
        var issued = await factory.WithDbAsync(db => db.IssuedPdfs.SingleAsync(x => x.DealId == dealId));
        Assert.True(issued.PageCount >= 1);

        // Повторный запуск сидера не создаёт вторую сделку.
        using (var scope = factory.Services.CreateScope())
            await DemoAccountsSeeder.SeedAsync(scope.ServiceProvider, NullLogger.Instance);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().CountAsync(d => d.Customer.Email == DemoAccountsSeeder.BuyerEmail)));
    }

    [Fact]
    public async Task DemoManager_ReadsAndChats_ButCannotChangeAnything()
    {
        var manager = await factory.CreateClientForAsync(DemoAccountsSeeder.ManagerEmail, DemoAccountsFactory.DemoManagerPassword);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/leads")).StatusCode);

        var lead = await manager.PostAsJsonAsync("/api/v1/leads", new CreateLeadRequestBody("Тест", "+992900001111"));
        Assert.Equal(HttpStatusCode.Forbidden, lead.StatusCode);
        var problem = await lead.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("demo_read_only", problem.GetProperty("code").GetString());

        var dealId = await factory.WithDbAsync(db => db.Deals.IgnoreQueryFilters().Where(d => d.Customer.Email == DemoAccountsSeeder.BuyerEmail).Select(d => d.Id).SingleAsync());
        var paymentId = await factory.WithDbAsync(db => db.Payments.IgnoreQueryFilters().Where(p => p.DealId == dealId).Select(p => p.Id).FirstAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/api/v1/payments/{paymentId}/reverse", new { reason = "тест" })).StatusCode);

        var message = await manager.PostAsJsonAsync($"/api/v1/deals/{dealId}/messages", new { text = "Сообщение от демо-менеджера" });
        Assert.True(message.IsSuccessStatusCode, $"{(int)message.StatusCode}: {await message.Content.ReadAsStringAsync()}");
    }

    private sealed record CreateLeadRequestBody(string Name, string Phone);
}

public class DemoPasswordLoginOffTests(DemoAccountsNoPasswordLoginFactory factory) : IClassFixture<DemoAccountsNoPasswordLoginFactory>
{
    [Fact]
    public async Task WithoutFlag_DemoBuyerCannotUsePassword()
    {
        var client = factory.CreateClient();
        var methods = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.False(methods.GetProperty("demo").GetBoolean());

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(DemoAccountsSeeder.BuyerEmail, DemoAccountsFactory.BuyerPassword));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // Демо-менеджер — сотрудник: входит паролем независимо от флага.
        Assert.NotNull(await factory.CreateClientForAsync(DemoAccountsSeeder.ManagerEmail, DemoAccountsFactory.DemoManagerPassword));
    }
}
