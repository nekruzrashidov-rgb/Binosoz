using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Сотрудники демо-организации из секретов сидера SEED_STAFF_* (docs/ROLES_LOGIN.md): вход под каждой ролью.
public class StaffAccountsFactory : SitoraApiFactory
{
    public const string ObjectAdminEmail = "seed-objectadmin@sitora.local";
    public const string AccountantEmail2 = "seed-buh@sitora.local";
    public const string ManagerEmail2 = "seed-manager@sitora.local";
    public const string ContentEmail2 = "seed-content@sitora.local";
    public string ObjectAdminPassword { get; } = StaffAccountsSeederTests.NewPassword();
    public string Accountant2Password { get; } = StaffAccountsSeederTests.NewPassword();
    public string Manager2Password { get; } = StaffAccountsSeederTests.NewPassword();
    public string Content2Password { get; } = StaffAccountsSeederTests.NewPassword();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("SEED_STAFF_OBJECTADMIN_EMAIL", ObjectAdminEmail);
        builder.UseSetting("SEED_STAFF_OBJECTADMIN_PASSWORD", ObjectAdminPassword);
        builder.UseSetting("SEED_STAFF_MANAGER_EMAIL", ManagerEmail2);
        builder.UseSetting("SEED_STAFF_MANAGER_PASSWORD", Manager2Password);
        builder.UseSetting("SEED_STAFF_ACCOUNTANT_EMAIL", AccountantEmail2);
        builder.UseSetting("SEED_STAFF_ACCOUNTANT_PASSWORD", Accountant2Password);
        builder.UseSetting("SEED_STAFF_CONTENT_EMAIL", ContentEmail2);
        builder.UseSetting("SEED_STAFF_CONTENT_PASSWORD", Content2Password);
    }
}

public class StaffAccountsSeederTests(StaffAccountsFactory factory) : IClassFixture<StaffAccountsFactory>
{
    public static string NewPassword() => $"Seed-{Guid.NewGuid():N}-Aa1";

    [Fact]
    public async Task EachRole_SignsIn_AndSeesOnlyItsSections()
    {
        // Owner — всё в организации, включая журнал и пользователей.
        var owner = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/v1/users")).StatusCode);
        var neighbour = await TestScenarios.ReadAsync<BuildingResponse>(await owner.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("Соседний сидера", "Ҳамсоя", "ул. Соседняя, 3", 5, null, null)));
        var ownerBuildings = await owner.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        Assert.Contains(neighbour.Id, ownerBuildings!.Items.Select(b => b.Id));

        // ObjectAdmin — только дом «Некруз Резиденс»: пользователи его дома есть, журнала организации нет.
        var objectAdmin = await factory.CreateClientForAsync(StaffAccountsFactory.ObjectAdminEmail, factory.ObjectAdminPassword);
        var me = await objectAdmin.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options);
        Assert.Equal((RoleNames.ObjectAdmin, Organization.DefaultId), (me!.Role, me.OrganizationId));
        var nekruz = await factory.WithDbAsync(db => db.Buildings.IgnoreQueryFilters()
            .Where(b => b.OrganizationId == Organization.DefaultId && b.NameRu == CatalogSeeder.BuildingName).Select(b => b.Id).SingleAsync());
        Assert.Equal([nekruz], me.BuildingIds);
        var buildings = await objectAdmin.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        Assert.Equal([nekruz], buildings!.Items.Select(b => b.Id));
        Assert.Equal(HttpStatusCode.OK, (await objectAdmin.GetAsync("/api/v1/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await objectAdmin.GetAsync("/api/v1/audit")).StatusCode);

        // Manager — лиды и продажи (полноценный, не демо «только чтение»), без журнала и пользователей.
        var manager = await factory.CreateClientForAsync(StaffAccountsFactory.ManagerEmail2, factory.Manager2Password);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/leads")).StatusCode);
        Assert.True((await manager.PostAsJsonAsync("/api/v1/leads", new { name = "Лид сидера", phone = SitoraApiFactory.RandomPhone() })).IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/users")).StatusCode);

        // Accountant — платежи (сторно несуществующего платежа — 404, а не 403), без лидов и каталога.
        var accountant = await factory.CreateClientForAsync(StaffAccountsFactory.AccountantEmail2, factory.Accountant2Password);
        Assert.Equal(HttpStatusCode.NotFound, (await accountant.PostAsJsonAsync($"/api/v1/payments/{Guid.NewGuid()}/reverse", new { reason = "тест" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/v1/leads")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PutAsJsonAsync($"/api/v1/buildings/{nekruz}/location", new { })).StatusCode);

        // Content — каталог (правка несуществующего дома — 404, а не 403), без платежей и лидов.
        var content = await factory.CreateClientForAsync(StaffAccountsFactory.ContentEmail2, factory.Content2Password);
        Assert.Equal(HttpStatusCode.NotFound, (await content.PutAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}",
            new SaveBuildingRequest("Нет", "Нест", "адрес", 5, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await content.PostAsJsonAsync($"/api/v1/payments/{Guid.NewGuid()}/reverse", new { reason = "тест" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await content.GetAsync("/api/v1/leads")).StatusCode);

        // Имя — часть почты, а не «Бухгалтер» по-русски на всех языках (как R-02).
        Assert.Equal("seed-buh", await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.AccountantEmail2).Select(u => u.FullName).SingleAsync()));

        // Создание записано в журнал.
        var created = await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.Action == SeedPasswords.CreatedAction));
        Assert.True(created >= 4);
    }

    [Fact]
    public async Task OldRussianSeederName_IsReplaced_OwnNameIsKept()
    {
        await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.ContentEmail2)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, "Контент-менеджер")));
        await RunSeederAsync(null);
        Assert.Equal("seed-content", await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.ContentEmail2).Select(u => u.FullName).SingleAsync()));

        await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.ContentEmail2)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.FullName, "Зарина Каримова")));
        await RunSeederAsync(null);
        Assert.Equal("Зарина Каримова", await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.ContentEmail2).Select(u => u.FullName).SingleAsync()));
    }

    [Fact]
    public async Task SecondRun_DoesNotDuplicate_AndKeepsPassword()
    {
        await RunSeederAsync(null);
        await RunSeederAsync(null);

        foreach (var email in new[] { StaffAccountsFactory.ObjectAdminEmail, StaffAccountsFactory.ManagerEmail2, StaffAccountsFactory.AccountantEmail2, StaffAccountsFactory.ContentEmail2 })
            Assert.Equal(1, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email)));
        Assert.Equal(1, await factory.WithDbAsync(db => db.UserBuildings.CountAsync(x => x.User.Email == StaffAccountsFactory.ObjectAdminEmail)));
        var contentId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == StaffAccountsFactory.ContentEmail2).Select(u => u.Id).SingleAsync());
        Assert.Equal(0, await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().CountAsync(a => a.Action == SeedPasswords.PasswordSetAction
            && a.EntityId == contentId)));
        Assert.NotNull(await factory.CreateClientForAsync(StaffAccountsFactory.ContentEmail2, factory.Content2Password));
    }

    [Fact]
    public async Task ChangedSecret_ChangesPassword_WritesAudit_AndRevokesSessions()
    {
        var email = $"rotate-{Guid.NewGuid():N}@sitora.local";
        var first = NewPassword();
        await RunSeederAsync(new() { ["SEED_STAFF_ACCOUNTANT_EMAIL"] = email, ["SEED_STAFF_ACCOUNTANT_PASSWORD"] = first });
        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, first));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth?.RefreshToken);

        var second = NewPassword();
        await RunSeederAsync(new() { ["SEED_STAFF_ACCOUNTANT_EMAIL"] = email, ["SEED_STAFF_ACCOUNTANT_PASSWORD"] = second });

        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        Assert.Equal(0, await factory.WithDbAsync(db => db.RefreshTokens.CountAsync(t => t.UserId == userId && t.RevokedAt == null)));
        var audit = await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().SingleAsync(a => a.EntityId == userId && a.Action == SeedPasswords.PasswordSetAction));
        Assert.Equal(Organization.DefaultId, audit.OrganizationId);
        Assert.DoesNotContain(second, audit.After ?? "");

        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth!.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.False((await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, first))).IsSuccessStatusCode);
        Assert.NotNull(await factory.CreateClientForAsync(email, second));
    }

    [Fact]
    public async Task WithoutPair_NothingIsCreated()
    {
        var email = $"half-{Guid.NewGuid():N}@sitora.local";
        await RunSeederAsync(new() { ["SEED_STAFF_CONTENT_EMAIL"] = email }, onlyOverride: true);
        await RunSeederAsync([], onlyOverride: true);
        Assert.Equal(0, await factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email)));
    }

    [Fact]
    public async Task EmailOfAnotherUser_FailsWithoutPasswordInMessage()
    {
        var password = NewPassword();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunSeederAsync(
            new() { ["SEED_STAFF_CONTENT_EMAIL"] = SitoraApiFactory.ManagerEmail, ["SEED_STAFF_CONTENT_PASSWORD"] = password }, onlyOverride: true));
        Assert.Contains("уже занята", error.Message);
        Assert.DoesNotContain(password, error.Message);
        // Пароль менеджера не тронут.
        Assert.NotNull(await factory.CreateManagerClientAsync());
    }

    [Fact]
    public async Task WeakPassword_FailsWithoutValueInMessage_AndKeepsOldPassword()
    {
        var weak = "abc";
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => RunSeederAsync(
            new() { ["SEED_STAFF_CONTENT_EMAIL"] = StaffAccountsFactory.ContentEmail2, ["SEED_STAFF_CONTENT_PASSWORD"] = weak }, onlyOverride: true));
        Assert.DoesNotContain(weak, error.Message);
        Assert.NotNull(await factory.CreateClientForAsync(StaffAccountsFactory.ContentEmail2, factory.Content2Password));
    }

    // Запуск сидера с другими секретами: поверх настроек фабрики или только с переданными.
    private async Task RunSeederAsync(Dictionary<string, string?>? overrides, bool onlyOverride = false)
    {
        using var scope = factory.Services.CreateScope();
        var builder = new ConfigurationBuilder();
        if (!onlyOverride)
            builder.AddConfiguration(scope.ServiceProvider.GetRequiredService<IConfiguration>());
        if (overrides is not null)
            builder.AddInMemoryCollection(overrides);
        await StaffAccountsSeeder.SeedAsync(new ConfigOverride(scope.ServiceProvider, builder.Build()), NullLogger.Instance);
    }

    private sealed class ConfigOverride(IServiceProvider inner, IConfiguration configuration) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IConfiguration) ? configuration : inner.GetService(serviceType);
    }
}
