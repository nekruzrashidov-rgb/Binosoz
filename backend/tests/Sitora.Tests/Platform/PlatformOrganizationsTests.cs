using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Управление организациями суперадмином: список со статистикой, карточка, изменение, приостановка, приглашение владельца.
public class PlatformOrganizationsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task List_ShowsCountsOwnersTariffAndActivity()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        await TestScenarios.CreateDealAsync(factory);
        var plans = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!;
        (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{Organization.DefaultId}/tariff",
            new AssignTariffRequest(plans.Single(p => p.Name == "Pro").Id, null, true), TestJson.Options)).EnsureSuccessStatusCode();

        var list = (await platform.GetFromJsonAsync<List<OrganizationResponse>>("/api/v1/platform/organizations", TestJson.Options))!;
        var sitora = Assert.Single(list, o => o.Id == Organization.DefaultId);

        Assert.True(sitora.DealsCount > 0);
        Assert.True(sitora.CustomersCount > 0);
        Assert.True(sitora.BuildingsCount > 0);
        Assert.NotNull(sitora.LastActivityAt);
        Assert.Equal(("Pro", true), (sitora.Tariff!.TariffName, sitora.Tariff.IsPaid));
        Assert.Contains(sitora.Owners!, o => o.Email == SitoraApiFactory.AdminEmail && o.FullName.Length > 0);
    }

    [Fact]
    public async Task Details_Update_SuspendActivate_AndAudit()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, slug) = await NewOrganizationAsync(platform);

        var details = await TestScenarios.ReadAsync<OrganizationDetailsResponse>(await platform.GetAsync($"/api/v1/platform/organizations/{id}"));
        Assert.Equal((slug, 0, 0m), (details.Organization.Slug, details.ApartmentsCount, details.PaymentsSumTjs));
        Assert.Single(details.Organization.Owners!);

        var renamed = await TestScenarios.ReadAsync<OrganizationDetailsResponse>(await platform.PutAsJsonAsync($"/api/v1/platform/organizations/{id}",
            new UpdateOrganizationRequest("Новое имя", slug + "-x")));
        Assert.Equal(("Новое имя", slug + "-x"), (renamed.Organization.Name, renamed.Organization.Slug));
        Assert.Equal(HttpStatusCode.Conflict, (await platform.PutAsJsonAsync($"/api/v1/platform/organizations/{id}",
            new UpdateOrganizationRequest("Захват", Organization.DefaultSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PutAsJsonAsync($"/api/v1/platform/organizations/{id}",
            new UpdateOrganizationRequest(" "))).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await platform.DeleteAsync($"/api/v1/platform/organizations/{id}/suspend")).StatusCode);
        Assert.False((await TestScenarios.ReadAsync<OrganizationDetailsResponse>(await platform.GetAsync($"/api/v1/platform/organizations/{id}"))).Organization.IsActive);
        Assert.Equal(HttpStatusCode.NoContent, (await platform.PostAsync($"/api/v1/platform/organizations/{id}/activate", null)).StatusCode);
        Assert.True((await TestScenarios.ReadAsync<OrganizationDetailsResponse>(await platform.GetAsync($"/api/v1/platform/organizations/{id}"))).Organization.IsActive);

        var actions = await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().Where(l => l.EntityId == id).Select(l => l.Action).ToListAsync());
        Assert.Contains("Updated", actions);
        Assert.Contains("Suspended", actions);
        Assert.Contains("Activated", actions);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.GetAsync($"/api/v1/platform/organizations/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task InviteOwner_CreatesOwnerWithEmailedPassword_OnlySuperAdmin()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, slug) = await NewOrganizationAsync(platform);
        var email = $"second-{slug}@org.test";

        var owner = await TestScenarios.ReadAsync<UserResponse>(await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{id}/invite-owner",
            new InviteOwnerRequest(email, "Второй", "Владелец")));
        Assert.Equal(("Второй Владелец", id), (owner.FullName, owner.OrganizationId));
        var client = await factory.CreateClientForAsync(email, factory.Email.LastPassword(email)!);
        Assert.Equal("Owner", (await client.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Role);

        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{id}/invite-owner",
            new InviteOwnerRequest(email, "Повтор", "Владелец"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{id}/invite-owner",
            new InviteOwnerRequest($"x-{slug}@org.test", "Без", " "))).StatusCode);

        var admin = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync($"/api/v1/platform/organizations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.DeleteAsync($"/api/v1/platform/organizations/{id}/suspend")).StatusCode);
    }

    private static async Task<(Guid Id, string Slug)> NewOrganizationAsync(HttpClient platform)
    {
        var slug = "o-" + Guid.NewGuid().ToString("N")[..10];
        var organization = await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Орг " + slug, slug, $"owner-{slug}@org.test", "Владелец Орг")));
        return (organization.Id, slug);
    }
}
