using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Пользователи всей платформы: фильтры, последний вход, блокировка, новый пароль.
public class PlatformUsersTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task List_FiltersByRoleOrganizationSearch_ShowsLastLoginAndDeals()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        await factory.CreateManagerClientAsync();
        var deal = await TestScenarios.CreateDealAsync(factory);
        await factory.CreateCustomerClientAsync(deal.Phone);

        var managers = await platform.GetFromJsonAsync<PagedResult<PlatformUserResponse>>(
            $"/api/v1/platform/users?role=manager&organizationId={Organization.DefaultId}&search=manager@sitora", TestJson.Options);
        var manager = Assert.Single(managers!.Items);
        Assert.Equal(("Manager", "Active", Organization.DefaultId), (manager.Role, manager.Status, manager.OrganizationId));
        Assert.NotNull(manager.LastLoginAt);
        Assert.False(string.IsNullOrEmpty(manager.OrganizationName));

        var customers = await platform.GetFromJsonAsync<PagedResult<PlatformUserResponse>>("/api/v1/platform/users?role=Customer&pageSize=100", TestJson.Options);
        Assert.Contains(customers!.Items, c => c.DealsCount > 0 && c.OrganizationId == null);
        Assert.All(customers.Items, c => Assert.Equal("Customer", c.Role));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.GetAsync("/api/v1/platform/users?role=Wizard")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await platform.GetAsync($"/api/v1/platform/users/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Block_ClosesLoginAndSessions_Unblock_OpensAgain_Audited()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, email, password) = await NewStaffAsync(platform);
        var session = await TestScenarios.ReadAsync<AuthResponse>(await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password)));

        var blocked = await TestScenarios.ReadAsync<PlatformUserResponse>(await platform.PostAsync($"/api/v1/platform/users/{id}/block", null));
        Assert.Equal("Blocked", blocked.Status);
        var lockout = await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.LockoutEnd).SingleAsync());
        Assert.True(lockout > DateTimeOffset.UtcNow.AddYears(99));
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.RefreshToken))).StatusCode);

        Assert.Equal("Active", (await TestScenarios.ReadAsync<PlatformUserResponse>(await platform.PostAsync($"/api/v1/platform/users/{id}/unblock", null))).Status);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);

        var actions = await factory.WithDbAsync(db => db.AuditLogs.IgnoreQueryFilters().Where(l => l.EntityId == id).Select(l => l.Action).ToListAsync());
        Assert.Contains("Blocked", actions);
        Assert.Contains("Unblocked", actions);

        // Себя и другого суперадмина — нельзя.
        var selfId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == SitoraApiFactory.SuperAdminEmail).Select(u => u.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsync($"/api/v1/platform/users/{selfId}/block", null)).StatusCode);
    }

    [Fact]
    public async Task ResetPassword_EmailsNewOne_OldStopsWorking_CustomerRejected()
    {
        var platform = await factory.CreateSuperAdminClientAsync();
        var (id, email, oldPassword) = await NewStaffAsync(platform);

        Assert.Equal(HttpStatusCode.NoContent, (await platform.PostAsync($"/api/v1/platform/users/{id}/reset-password", null)).StatusCode);
        Assert.Equal("Binosoz: новый пароль", factory.Email.LastMessage(email)!.Subject);
        var newPassword = factory.Email.LastPassword(email)!;
        Assert.NotEqual(oldPassword, newPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, oldPassword))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, newPassword))).StatusCode);

        var deal = await TestScenarios.CreateDealAsync(factory);
        var customer = await factory.CreateCustomerClientAsync(deal.Phone);
        var customerId = (await customer.GetFromJsonAsync<CurrentUserResponse>("/api/v1/auth/me", TestJson.Options))!.Id;
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await platform.PostAsync($"/api/v1/platform/users/{customerId}/reset-password", null)).StatusCode);

        var owner = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync("/api/v1/platform/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PostAsync($"/api/v1/platform/users/{id}/block", null)).StatusCode);
    }

    // Сотрудник новой организации: пароль из письма приглашения.
    private async Task<(Guid Id, string Email, string Password)> NewStaffAsync(HttpClient platform)
    {
        var slug = "u-" + Guid.NewGuid().ToString("N")[..10];
        var email = $"owner-{slug}@users.test";
        var organization = await TestScenarios.ReadAsync<OrganizationResponse>(await platform.PostAsJsonAsync("/api/v1/platform/organizations",
            new CreateOrganizationRequest("Польз " + slug, slug, email, "Владелец Польз")));
        var id = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        Assert.Equal(organization.Id, await factory.WithDbAsync(db => db.Users.Where(u => u.Id == id).Select(u => u.OrganizationId).SingleAsync()));
        return (id, email, factory.Email.LastPassword(email)!);
    }
}
