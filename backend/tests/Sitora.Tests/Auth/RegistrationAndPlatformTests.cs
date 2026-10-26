using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

public class RegistrationAndPlatformTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_CreatesCustomerInOrganization_AndCodeSignsInAsCustomer()
    {
        var email = $"Buyer-{Guid.NewGuid():N}@Mail.Test";
        var normalized = email.ToLowerInvariant();
        var phone = SitoraApiFactory.RandomPhone();

        var response = await Register(email, "Новый Покупатель", phone);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var code = factory.Email.LastCode(normalized);
        Assert.NotNull(code);
        var customer = await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Email == normalized));
        Assert.Equal((Organization.DefaultId, "Новый Покупатель", phone), (customer.OrganizationId, customer.FullName, customer.Phone));

        var auth = await TestScenarios.ReadAsync<AuthResponse>(await _client.PostAsJsonAsync("/api/v1/auth/email/verify",
            new EmailVerifyRequest(normalized, code!)));
        Assert.Equal("Customer", auth.User.Role);

        // Сотрудник застройщика видит нового клиента в CRM.
        var manager = await factory.CreateManagerClientAsync();
        var list = await manager.GetFromJsonAsync<PagedResult<CustomerListItem>>($"/api/v1/customers?search={Uri.EscapeDataString(phone)}", TestJson.Options);
        Assert.Contains(list!.Items, c => c.Id == customer.Id);

        // Повторная регистрация той же почты просто присылает код.
        await factory.WithDbAsync(db => db.EmailCodes.Where(c => c.Email == normalized)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, c => c.CreatedAt.AddMinutes(-2))));
        Assert.Equal(HttpStatusCode.OK, (await Register(email, "Новый Покупатель", phone)).StatusCode);
        Assert.Equal(1, await factory.WithDbAsync(db => db.Customers.IgnoreQueryFilters().CountAsync(c => c.Email == normalized)));
    }

    [Fact]
    public async Task Register_ForeignPhone_UnknownOrganization_AndBadInput_AreRejected()
    {
        // Чужой телефон к новой почте не привязывается: иначе по номеру можно было бы получить чужие сделки.
        var deal = await TestScenarios.CreateDealAsync(factory);
        var taken = await Register($"thief-{Guid.NewGuid():N}@mail.test", "Чужой", deal.Phone);
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Contains("другой почтой", await taken.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await Register($"x-{Guid.NewGuid():N}@mail.test", "Имя", SitoraApiFactory.RandomPhone(), "no-such-developer")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Register($"y-{Guid.NewGuid():N}@mail.test", "Имя", "12345")).StatusCode);
    }

    [Fact]
    public async Task PlatformLogin_OnlyForPlatformOwner_AndStillAsksSecondFactor()
    {
        await Send(SitoraApiFactory.PlatformOwnerEmail);
        var platform = await _client.PostAsJsonAsync("/api/v1/auth/platform/login",
            new EmailVerifyRequest(SitoraApiFactory.PlatformOwnerEmail, factory.Email.LastCode(SitoraApiFactory.PlatformOwnerEmail)!));
        Assert.Equal(HttpStatusCode.Accepted, platform.StatusCode);
        Assert.False(string.IsNullOrEmpty((await platform.Content.ReadFromJsonAsync<SecondFactorChallenge>(TestJson.Options))!.ChallengeToken));

        // Сотрудник через этот вход не попадёт, и его код при этом не тратится.
        await Send(SitoraApiFactory.ContentEmail);
        var code = factory.Email.LastCode(SitoraApiFactory.ContentEmail)!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/platform/login",
            new EmailVerifyRequest(SitoraApiFactory.ContentEmail, code))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/auth/email/verify",
            new EmailVerifyRequest(SitoraApiFactory.ContentEmail, code))).StatusCode);
    }

    [Fact]
    public async Task PlatformOwner_AddsOwnerWithPassword_AndOthersCannot()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var slug = $"o-{Guid.NewGuid():N}"[..16];
        var organization = await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Второй застройщик", slug, $"first-{slug}@org.test", "Первый владелец")));
        var email = $"second-{slug}@org.test";
        const string password = "Second-Owner-1";

        var added = await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{organization.Id}/owners",
            new AddOwnerRequest(email, "Второй владелец", password));
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(organization.Id, (await added.Content.ReadFromJsonAsync<UserResponse>(TestJson.Options))!.OrganizationId);

        var owner = await factory.CreateClientForAsync(email, password);
        var me = await owner.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.Equal(("Owner", organization.Id), (me!.Role, me.OrganizationId));
        Assert.Equal(0, (await owner.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options))!.TotalCount);

        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{organization.Id}/owners",
            new AddOwnerRequest(email, "Повтор", null))).StatusCode);
        var admin = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/platform/organizations/{organization.Id}/owners",
            new AddOwnerRequest($"z-{slug}@org.test", "Захват", null))).StatusCode);
    }

    [Fact]
    public async Task PlatformOwner_ImpersonatesAUser_SeesTheirData_AndItIsAudited()
    {
        var deal = await TestScenarios.CreateDealAsync(factory);
        var customerClient = await factory.CreateCustomerClientAsync(deal.Phone);
        var customerId = (await customerClient.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Id;
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var platformId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.PlatformOwnerEmail).Select(u => u.Id).SingleAsync());

        var started = await TestScenarios.ReadAsync<AccessTokenResponse>(await platform.PostAsync($"/api/v1/platform/impersonate/{customerId}", null));
        Assert.Equal(("Customer", customerId), (started.User.Role, started.User.Id));
        Assert.Equal(platformId, started.User.Impersonation!.ImpersonatedBy);

        var asCustomer = factory.CreateClient();
        asCustomer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", started.AccessToken);
        var deals = await asCustomer.GetFromJsonAsync<List<DealResponse>>("/api/v1/me/deals", TestJson.Options);
        Assert.Equal([deal.DealId], deals!.Select(d => d.Id));
        Assert.NotNull((await asCustomer.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Impersonation);

        var stopped = await TestScenarios.ReadAsync<AccessTokenResponse>(await asCustomer.PostAsync("/api/v1/platform/impersonate/stop", null));
        Assert.Equal("PlatformOwner", stopped.User.Role);

        await factory.WithDbAsync(async db =>
        {
            var rows = await db.AuditLogs.IgnoreQueryFilters().Where(a => a.EntityId == customerId).ToListAsync();
            Assert.Contains(rows, a => a.Action == "ImpersonationStarted");
            Assert.Contains(rows, a => a.Action == "ImpersonationStopped");
        });

        // Под создателем платформы войти нельзя; несуществующий id — 404.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsync($"/api/v1/platform/impersonate/{platformId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.PostAsync($"/api/v1/platform/impersonate/{Guid.NewGuid()}", null)).StatusCode);
    }

    private Task<HttpResponseMessage> Register(string email, string fullName, string phone, string? organization = null) =>
        _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterCustomerRequest(email, fullName, phone, organization ?? Organization.DefaultSlug));

    private async Task Send(string email) =>
        (await _client.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(email))).EnsureSuccessStatusCode();
}

// Сидер разработки со вторым застройщиком: Rich Tower, его владелец и дом.
public class RichTowerFactory : SitoraApiFactory
{
    public string RichTowerPassword { get; } = "Rich-" + Guid.NewGuid().ToString("N")[..10] + "A1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Seed:RichTower", "true");
        builder.UseSetting(RichTowerSeeder.OwnerPasswordVariable, RichTowerPassword);
    }
}

public class RichTowerSeedTests(RichTowerFactory factory) : IClassFixture<RichTowerFactory>
{
    [Fact]
    public async Task RichTowerOwner_SeesOnlyOwnBuilding_AndDefaultOwnerDoesNotSeeIt()
    {
        var owner = await factory.CreateClientForAsync(RichTowerSeeder.OwnerEmail, factory.RichTowerPassword);
        var me = await owner.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.Equal(("Owner", "Rich Tower"), (me!.Role, me.OrganizationName));

        var own = await owner.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        var building = Assert.Single(own!.Items);
        Assert.Equal("Rich Tower", building.NameRu);

        var admin = await factory.CreateAdminClientAsync();
        var adminBuildings = await admin.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?pageSize=100", TestJson.Options);
        Assert.DoesNotContain(adminBuildings!.Items, b => b.Id == building.Id);

        var publicSite = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>(
            $"/api/v1/buildings?organization={RichTowerSeeder.Slug}", TestJson.Options);
        Assert.Equal([building.Id], publicSite!.Items.Select(b => b.Id));
    }
}
