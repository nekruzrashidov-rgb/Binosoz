using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Тарифы платформы: базовые тарифы, создание и изменение, назначение организации и лимиты (402).
public class TariffServiceTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task SeededPlans_FreeBasicPro_WithLimits()
    {
        var platform = await factory.CreateSuperAdminClientAsync();

        var plans = (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!;

        var byName = plans.ToDictionary(p => p.Name);
        Assert.Equal((0m, 1, 5), (byName["Free"].Price, byName["Free"].MaxBuildings, byName["Free"].MaxUsers));
        Assert.Equal((500m, 3, 20), (byName["Basic"].Price, byName["Basic"].MaxBuildings, byName["Basic"].MaxUsers));
        Assert.Equal((1500m, (int?)null, (int?)null), (byName["Pro"].Price, byName["Pro"].MaxBuildings, byName["Pro"].MaxUsers));
        Assert.All(plans, p => Assert.Equal(Currency.TJS, p.Currency));
        Assert.NotEmpty(byName["Pro"].Features);
    }

    [Fact]
    public async Task Create_Update_DuplicateName_AndValidation()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var name = "Тест-" + Guid.NewGuid().ToString("N")[..8];

        var created = await TestScenarios.ReadAsync<TariffPlanResponse>(await platform.PostAsJsonAsync("/api/v1/platform/tariffs",
            new SaveTariffRequest(name, 990m, Currency.TJS, 2, 10, ["Две функции", " "]), TestJson.Options));
        Assert.Equal((990m, 2, 10), (created.Price, created.MaxBuildings, created.MaxUsers));
        Assert.Equal(["Две функции"], created.Features);

        var updated = await TestScenarios.ReadAsync<TariffPlanResponse>(await platform.PutAsJsonAsync($"/api/v1/platform/tariffs/{created.Id}",
            new SaveTariffRequest(name, 1200m, Currency.USD, null, 15, [], false), TestJson.Options));
        Assert.Equal((1200m, Currency.USD, (int?)null, false), (updated.Price, updated.Currency, updated.MaxBuildings, updated.IsActive));

        Assert.Equal(HttpStatusCode.Conflict, (await platform.PostAsJsonAsync("/api/v1/platform/tariffs", new SaveTariffRequest("Free", 1m), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync("/api/v1/platform/tariffs", new SaveTariffRequest("Минус", -5m), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync("/api/v1/platform/tariffs", new SaveTariffRequest("Ноль домов", 5m, Currency.TJS, 0), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.PutAsJsonAsync($"/api/v1/platform/tariffs/{Guid.NewGuid()}", new SaveTariffRequest("Нет", 1m), TestJson.Options)).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == created.Id && l.Action == "Updated")));
    }

    [Fact]
    public async Task OnlySuperAdmin_ManagesTariffs()
    {
        var owner = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/v1/platform/tariffs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsJsonAsync("/api/v1/platform/tariffs", new SaveTariffRequest("Захват", 0m), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/platform/tariffs")).StatusCode);
    }

    [Fact]
    public async Task Assign_ClosesPrevious_InactivePlanRejected()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (organizationId, _, _) = await NewOrganizationAsync(platform);
        var plans = await PlansAsync(platform);

        var first = await Assign(platform, organizationId, plans["Basic"].Id, isPaid: true);
        var second = await Assign(platform, organizationId, plans["Pro"].Id);
        Assert.Equal(("Pro", false), (second.TariffName, second.IsPaid));
        await factory.WithDbAsync(async db =>
        {
            var closed = await db.OrganizationTariffs.SingleAsync(t => t.Id == first.Id);
            Assert.NotNull(closed.EndsAt);
            Assert.Null((await db.OrganizationTariffs.SingleAsync(t => t.Id == second.Id)).EndsAt);
        });

        var inactive = await TestScenarios.ReadAsync<TariffPlanResponse>(await platform.PostAsJsonAsync("/api/v1/platform/tariffs",
            new SaveTariffRequest("Архив-" + Guid.NewGuid().ToString("N")[..6], 1m, IsActive: false), TestJson.Options));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{organizationId}/tariff",
            new AssignTariffRequest(inactive.Id), TestJson.Options)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{Guid.NewGuid()}/tariff",
            new AssignTariffRequest(plans["Pro"].Id), TestJson.Options)).StatusCode);
    }

    [Fact]
    public async Task FreePlan_LimitsBuildingsAndUsers_With402()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (organizationId, ownerEmail, ownerPassword) = await NewOrganizationAsync(platform);
        var plans = await PlansAsync(platform);
        await Assign(platform, organizationId, plans["Free"].Id);
        var owner = await factory.CreateClientForAsync(ownerEmail, ownerPassword);

        // Один дом можно, второй — 402.
        (await owner.PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest("Первый", "Якум", "ул. 1", 5, null, null))).EnsureSuccessStatusCode();
        var second = await owner.PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest("Второй", "Дуюм", "ул. 2", 5, null, null));
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains("Перейдите на тариф выше", (await second.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);

        // Владелец — первый из 5 сотрудников: ещё четверо, шестой — 402.
        for (var i = 0; i < 4; i++)
            (await owner.PostAsJsonAsync("/api/v1/invitations", new CreateInvitationRequest($"s{i}-{Guid.NewGuid():N}@limit.test", "Сотрудник", $"Номер{i}", "Manager"))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest($"extra-{Guid.NewGuid():N}@limit.test", "Лишний", "Сотрудник", "Manager"))).StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, (await owner.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest($"extra2-{Guid.NewGuid():N}@limit.test", "Лишний Два", "Manager", null, null))).StatusCode);

        // Pro снимает ограничения.
        await Assign(platform, organizationId, plans["Pro"].Id);
        (await owner.PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest("Второй", "Дуюм", "ул. 2", 5, null, null))).EnsureSuccessStatusCode();
    }

    private static async Task<Dictionary<string, TariffPlanResponse>> PlansAsync(HttpClient platform) =>
        (await platform.GetFromJsonAsync<List<TariffPlanResponse>>("/api/v1/platform/tariffs", TestJson.Options))!.ToDictionary(p => p.Name);

    private static async Task<OrganizationTariffResponse> Assign(HttpClient platform, Guid organizationId, Guid planId, bool isPaid = false) =>
        await TestScenarios.ReadAsync<OrganizationTariffResponse>(await platform.PostAsJsonAsync($"/api/v1/platform/organizations/{organizationId}/tariff",
            new AssignTariffRequest(planId, null, isPaid), TestJson.Options));

    private async Task<(Guid Id, string OwnerEmail, string OwnerPassword)> NewOrganizationAsync(HttpClient platform)
    {
        var slug = "t-" + Guid.NewGuid().ToString("N")[..10];
        var ownerEmail = $"owner-{slug}@tariff.test";
        var organization = await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Тариф " + slug, slug, ownerEmail, "Владелец Тарифа")));
        return (organization.Id, ownerEmail, factory.Email.LastPassword(ownerEmail)!);
    }
}
