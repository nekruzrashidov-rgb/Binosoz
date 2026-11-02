using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Security;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Страница /accept-invite для ссылок из старых писем: сведения о приглашении и принятие — только с паролем.
// Новые приглашения ссылок не содержат (учётная запись создаётся сразу), поэтому приглашение кладём в базу сами.
public class InvitationPageTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Info_ShowsOrganizationAndRole_UntilAccepted()
    {
        var (email, token) = await LegacyInvitationAsync("Manager", "Приглашённый менеджер");

        var info = await TestScenarios.ReadAsync<InvitationInfoResponse>(await _guest.GetAsync($"/api/v1/auth/invitations/{token}"));
        Assert.Equal((email, "Приглашённый менеджер", "Manager"), (info.Email, info.FullName, info.Role));
        Assert.False(string.IsNullOrWhiteSpace(info.OrganizationName));

        (await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token, "Menedzher2026"))).EnsureSuccessStatusCode();
        var used = await _guest.GetAsync($"/api/v1/auth/invitations/{token}");
        Assert.Equal(HttpStatusCode.NotFound, used.StatusCode);
        Assert.Contains("Приглашение недействительно", await used.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Info_UnknownToken_Is404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _guest.GetAsync("/api/v1/auth/invitations/invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _guest.GetAsync($"/api/v1/auth/invitations/{new string('A', 64)}")).StatusCode);
    }

    [Fact]
    public async Task Accept_NeedsPassword_WeakOrMissingKeepsInvitation_ThenPasswordLoginWorks()
    {
        var (email, token) = await LegacyInvitationAsync("Accountant", "Приглашённый сотрудник");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token))).StatusCode);
        var weak = await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token, "short"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _guest.GetAsync($"/api/v1/auth/invitations/{token}")).StatusCode);

        var auth = await TestScenarios.ReadAsync<AuthResponse>(
            await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token, "Bukhgalter2026")));
        Assert.Equal("Accountant", auth.User.Role);

        var login = await _guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Bukhgalter2026"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private async Task<(string Email, string Token)> LegacyInvitationAsync(string role, string fullName)
    {
        var email = $"invited-{Guid.NewGuid():N}@sitora.local";
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        await factory.WithDbAsync(async db =>
        {
            var adminId = await db.Users.Where(u => u.Email == SitoraApiFactory.AdminEmail).Select(u => u.Id).SingleAsync();
            db.Invitations.Add(new Invitation
            {
                OrganizationId = Organization.DefaultId,
                Email = email,
                FullName = fullName,
                Role = role,
                BuildingIds = [],
                TokenHash = JwtTokenService.HashToken(token),
                ExpiresAt = DateTime.UtcNow.Add(Invitation.Lifetime),
                InvitedById = adminId
            });
            await db.SaveChangesAsync();
        });
        return (email, token);
    }
}
