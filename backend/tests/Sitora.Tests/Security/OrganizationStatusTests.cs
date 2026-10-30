using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Раздел «Застройщики» создателя платформы: подробности организации и приостановка.
public class OrganizationStatusTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Organizations_ListOwnersAndBuildings()
    {
        var (platform, slug, ownerEmail) = await NewOrganizationAsync();
        var owner = await factory.CreateEmailClientAsync(ownerEmail);
        (await owner.PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest("Дом статуса", "Хона", "ул. Тест, 1", 3, null, null))).EnsureSuccessStatusCode();

        var list = await platform.GetFromJsonAsync<List<OrganizationResponse>>("/api/v1/platform/organizations", TestJson.Options);
        var org = Assert.Single(list!, o => o.Slug == slug);
        Assert.Equal([ownerEmail], org.OwnerEmails!);
        Assert.Equal(["Дом статуса"], org.BuildingNames!);
        Assert.Equal(1, org.BuildingsCount);
    }

    [Fact]
    public async Task Suspend_ClosesSignInAndSessions_Activate_OpensAgain()
    {
        var (platform, slug, ownerEmail) = await NewOrganizationAsync();
        var signIn = factory.CreateClient();
        (await signIn.PostAsJsonAsync("/api/v1/auth/email/send", new EmailSendRequest(ownerEmail))).EnsureSuccessStatusCode();
        var auth = await TestScenarios.ReadAsync<AuthResponse>(await signIn.PostAsJsonAsync("/api/v1/auth/email/verify",
            new EmailVerifyRequest(ownerEmail, factory.Email.LastCode(ownerEmail)!)));
        var id = (await platform.GetFromJsonAsync<List<OrganizationResponse>>("/api/v1/platform/organizations", TestJson.Options))!.Single(o => o.Slug == slug).Id;

        Assert.Equal(HttpStatusCode.NoContent, (await platform.PatchAsJsonAsync($"/api/v1/platform/organizations/{id}/status", new OrganizationStatusRequest(false))).StatusCode);

        Assert.False(await factory.WithDbAsync(db => db.Organizations.IgnoreQueryFilters().Where(o => o.Id == id).Select(o => o.IsActive).SingleAsync()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await signIn.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(auth.RefreshToken))).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == id && l.Action == "Suspended")));

        Assert.Equal(HttpStatusCode.NoContent, (await platform.PatchAsJsonAsync($"/api/v1/platform/organizations/{id}/status", new OrganizationStatusRequest(true))).StatusCode);
        var again = await factory.CreateEmailClientAsync(ownerEmail);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Status_OnlyPlatformOwner_UnknownIs404()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var admin = await factory.CreateAdminClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await platform.PatchAsJsonAsync($"/api/v1/platform/organizations/{Guid.NewGuid()}/status", new OrganizationStatusRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PatchAsJsonAsync($"/api/v1/platform/organizations/{Guid.NewGuid()}/status", new OrganizationStatusRequest(false))).StatusCode);
    }

    private async Task<(HttpClient Platform, string Slug, string OwnerEmail)> NewOrganizationAsync()
    {
        var platform = await factory.CreatePlatformOwnerClientAsync();
        var slug = "st-" + Guid.NewGuid().ToString("N")[..8];
        var ownerEmail = $"owner-{slug}@status.test";
        (await platform.PostAsJsonAsync("/api/v1/platform/organizations", new CreateOrganizationRequest("Статус " + slug, slug, ownerEmail, "Владелец Статуса"))).EnsureSuccessStatusCode();
        return (platform, slug, ownerEmail);
    }
}
