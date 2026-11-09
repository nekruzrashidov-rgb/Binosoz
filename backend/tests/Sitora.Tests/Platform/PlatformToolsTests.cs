using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

public class PlatformToolsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Activity_ShowsAuditOfAllOrganizations_WithOrganizationNameAndFilters()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, slug) = await NewOrganizationAsync(platform);

        var page = (await platform.GetFromJsonAsync<PagedResult<PlatformActivityItem>>(
            $"/api/v1/platform/activity?organizationId={id}&action=Created&pageSize=50", TestJson.Options))!;
        var created = Assert.Single(page.Items, i => i.Entity == "Organization");
        Assert.Equal("РћСЂРі " + slug, created.OrganizationName);
        Assert.Equal(SitoraApiFactory.SuperAdminEmail, created.UserEmail);
        Assert.All(page.Items, i => Assert.Equal(id, i.OrganizationId));

        // Р‘СѓРґСѓС‰РёР№ РїРµСЂРёРѕРґ вЂ” РїСѓСЃС‚Рѕ; СЃРїРёСЃРѕРє РґРµР№СЃС‚РІРёР№ РґР»СЏ С„РёР»СЊС‚СЂР° Р·РЅР°РµС‚ В«CreatedВ».
        var future = (await platform.GetFromJsonAsync<PagedResult<PlatformActivityItem>>(
            $"/api/v1/platform/activity?organizationId={id}&from={DateTime.UtcNow.AddDays(1):O}", TestJson.Options))!;
        Assert.Empty(future.Items);
        Assert.Contains("Created", (await platform.GetFromJsonAsync<List<string>>("/api/v1/platform/activity/actions", TestJson.Options))!);

        var owner = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/v1/platform/activity")).StatusCode);
    }

    [Fact]
    public async Task Notification_GoesToEveryUserOfTheOrganization_AndIsValidated()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, _) = await NewOrganizationAsync(platform);

        var sent = await TestScenarios.ReadAsync<PlatformNotificationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/notifications",
            new PlatformNotificationRequest(id, "Warning", "РџР»Р°РЅРѕРІС‹Рµ СЂР°Р±РѕС‚С‹", "РќРѕС‡СЊСЋ РїР»Р°С‚С„РѕСЂРјР° Р±СѓРґРµС‚ РЅРµРґРѕСЃС‚СѓРїРЅР° 10 РјРёРЅСѓС‚")));
        Assert.Equal(1, sent.Recipients);

        var inbox = await factory.WithDbAsync(db => db.Notifications.IgnoreQueryFilters()
            .Where(n => n.Kind == "platform_warning" && db.Users.Any(u => u.Id == n.UserId && u.OrganizationId == id))
            .Select(n => new { n.Title, n.Text, n.Channel }).ToListAsync());
        var note = Assert.Single(inbox);
        Assert.Equal(("РџР»Р°РЅРѕРІС‹Рµ СЂР°Р±РѕС‚С‹", "InApp"), (note.Title, note.Channel));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync("/api/v1/platform/notifications",
            new PlatformNotificationRequest(id, "spam", "Рў", "Рў"))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync("/api/v1/platform/notifications",
            new PlatformNotificationRequest(id, "info", " ", "РўРµРєСЃС‚"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.PostAsJsonAsync("/api/v1/platform/notifications",
            new PlatformNotificationRequest(Guid.NewGuid(), "info", "Рў", "РўРµРєСЃС‚"))).StatusCode);
    }

    // Р›РёРјРёС‚ РѕСЂРіР°РЅРёР·Р°С†РёР№ РЅРµ СЃС‚Р°РІРёС‚СЃСЏ РЅРёР¶Рµ С‚РµРєСѓС‰РµРіРѕ С‡РёСЃР»Р° Рё СЃРЅРёРјР°РµС‚СЃСЏ; РґСЂСѓРіРёРµ С‚РµСЃС‚С‹ СЃРѕР·РґР°СЋС‚ РѕСЂРіР°РЅРёР·Р°С†РёРё РїР°СЂР°Р»Р»РµР»СЊРЅРѕ,
    // РїРѕСЌС‚РѕРјСѓ Р·РґРµСЃСЊ Р»РёРјРёС‚ СЃС‚Р°РІРёС‚СЃСЏ СЃ Р±РѕР»СЊС€РёРј Р·Р°РїР°СЃРѕРј Рё СЃСЂР°Р·Сѓ СЃРЅРёРјР°РµС‚СЃСЏ.
    [Fact]
    public async Task Settings_AreReadAndSaved_AndAudited()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var before = (await platform.GetFromJsonAsync<PlatformSettingsResponse>("/api/v1/platform/settings", TestJson.Options))!;
        Assert.False(string.IsNullOrWhiteSpace(before.Version));
        Assert.True(before.Organizations >= 1);

        var saved = await TestScenarios.ReadAsync<PlatformSettingsResponse>(await platform.PatchAsJsonAsync("/api/v1/platform/settings",
            new UpdatePlatformSettingsRequest(before.Organizations + 10_000, null, !before.DemoMode, " Support@Binosoz.TJ ")));
        Assert.Equal(before.Organizations + 10_000, saved.MaxOrganizations);
        Assert.Equal("support@binosoz.tj", saved.ContactEmail);
        Assert.Equal(!before.DemoMode, saved.DemoMode);
        var read = (await platform.GetFromJsonAsync<PlatformSettingsResponse>("/api/v1/platform/settings", TestJson.Options))!;
        Assert.Equal((saved.MaxOrganizations, saved.ContactEmail, saved.DemoMode), (read.MaxOrganizations, read.ContactEmail, read.DemoMode));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PatchAsJsonAsync("/api/v1/platform/settings",
            new UpdatePlatformSettingsRequest(0, null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PatchAsJsonAsync("/api/v1/platform/settings",
            new UpdatePlatformSettingsRequest(null, null, null, "РЅРµ РїРѕС‡С‚Р°"))).StatusCode);

        var restored = await TestScenarios.ReadAsync<PlatformSettingsResponse>(await platform.PatchAsJsonAsync("/api/v1/platform/settings",
            new UpdatePlatformSettingsRequest(null, true, before.DemoMode, before.ContactEmail ?? "")));
        Assert.Null(restored.MaxOrganizations);
        Assert.Equal(before.ContactEmail, restored.ContactEmail);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().AnyAsync(a => a.Entity == "PlatformSettings")));
    }

    private static async Task<(Guid Id, string Slug)> NewOrganizationAsync(HttpClient platform)
    {
        var slug = "t-" + Guid.NewGuid().ToString("N")[..10];
        var organization = await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("РћСЂРі " + slug, slug, $"owner-{slug}@org.test", "Р’Р»Р°РґРµР»РµС† РћСЂРі")));
        return (organization.Id, slug);
    }
}
