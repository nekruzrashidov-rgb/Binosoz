using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// «Забыли пароль?»: ссылка на почту сотрудника (24 часа, один раз), новый пароль закрывает все сессии.
public class PasswordResetTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Request_AnswersTheSame_MailsOnlyStaff()
    {
        var (staff, _) = await NewStaffAsync();
        var unknown = $"ghost-{Guid.NewGuid():N}@nowhere.test";
        var customer = $"buyer-{Guid.NewGuid():N}@mail.test";
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = new AppUser { UserName = customer, Email = customer, EmailConfirmed = true, FullName = "Покупатель" };
            await users.CreateAsync(user);
            await users.AddToRoleAsync(user, RoleNames.Customer);
        });

        var answers = new List<string>();
        foreach (var email in new[] { staff, unknown, customer })
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/password/reset-request", new PasswordResetRequest(email));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            answers.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.Single(answers.Distinct());
        Assert.NotNull(factory.Email.LastResetToken(staff));
        Assert.Contains("/login#reset=", factory.Email.LastMessage(staff)!.Text);
        Assert.Equal(0, factory.Email.SentCount(unknown));
        Assert.Null(factory.Email.LastResetToken(customer));
    }

    [Fact]
    public async Task Reset_SetsNewPassword_Once_ClosesSessions_AndIsAudited()
    {
        var (email, oldPassword) = await NewStaffAsync();
        var session = await TestScenarios.ReadAsync<AuthResponse>(await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, oldPassword)));
        var token = await RequestTokenAsync(email);

        // Слабый пароль — 422, а ссылка остаётся рабочей.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Reset(token, "short")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Reset(token, "onlylowercase1")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Reset(token, "New-Pass-2026")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "New-Pass-2026"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, oldPassword))).StatusCode);
        // Прежние сессии закрыты, ссылка второй раз не срабатывает.
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.RefreshToken))).StatusCode);
        var reused = await Reset(token, "Another-Pass-1");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reused.StatusCode);
        Assert.Contains("недействительна или устарела", await reused.Content.ReadAsStringAsync());

        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == session.User.Id && l.Action == "PasswordReset")));
    }

    [Fact]
    public async Task Reset_NewRequestCancelsOldLink_ExpiredAndUnknownTokensFail()
    {
        var (email, _) = await NewStaffAsync();
        var first = await RequestTokenAsync(email);
        var second = await RequestTokenAsync(email);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Reset(first, "New-Pass-2026")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Reset("not-a-token", "New-Pass-2026")).StatusCode);

        // Ссылка живёт 24 часа.
        var userId = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        var lifetime = await factory.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == userId && t.UsedAt == null)
            .Select(t => t.ExpiresAt - t.CreatedAt).SingleAsync());
        Assert.InRange(lifetime, TimeSpan.FromHours(23.9), TimeSpan.FromHours(24.1));
        await factory.WithDbAsync(db => db.PasswordResetTokens.Where(t => t.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Reset(second, "New-Pass-2026")).StatusCode);
    }

    [Fact]
    public async Task Reset_UnlocksAccountLockedByWrongPasswords()
    {
        var (email, _) = await NewStaffAsync();
        for (var i = 0; i < 5; i++)
            await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Wrong-pass-1"));

        Assert.Equal(HttpStatusCode.NoContent, (await Reset(await RequestTokenAsync(email), "New-Pass-2026")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "New-Pass-2026"))).StatusCode);
    }

    private async Task<string> RequestTokenAsync(string email)
    {
        (await _client.PostAsJsonAsync("/api/v1/auth/password/reset-request", new PasswordResetRequest(email))).EnsureSuccessStatusCode();
        return factory.Email.LastResetToken(email)!;
    }

    private Task<HttpResponseMessage> Reset(string token, string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/password/reset", new PasswordResetConfirmRequest(token, password));

    private async Task<(string Email, string Password)> NewStaffAsync()
    {
        var email = $"reset-{Guid.NewGuid():N}@sitora.test";
        const string password = "Staff-Pass-1";
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Сотрудник", OrganizationId = Organization.DefaultId };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, RoleNames.Accountant);
        });
        return (email, password);
    }
}
