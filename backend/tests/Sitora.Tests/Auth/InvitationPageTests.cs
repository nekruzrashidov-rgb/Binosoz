using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Страница /accept-invite: сведения о приглашении до принятия и принятие с паролем.
public class InvitationPageTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _guest = factory.CreateClient();

    [Fact]
    public async Task Info_ShowsOrganizationAndRole_UntilAccepted()
    {
        var (email, token) = await InviteAsync("Manager");

        var info = await TestScenarios.ReadAsync<InvitationInfoResponse>(await _guest.GetAsync($"/api/v1/auth/invitations/{token}"));
        Assert.Equal((email, "Приглашённый менеджер", "Manager"), (info.Email, info.FullName, info.Role));
        Assert.False(string.IsNullOrWhiteSpace(info.OrganizationName));

        (await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token))).EnsureSuccessStatusCode();
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
    public async Task Accept_WithPassword_AllowsPasswordLogin_AndWeakPasswordKeepsInvitation()
    {
        var (email, token) = await InviteAsync("Accountant");

        var weak = await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token, "short"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, weak.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _guest.GetAsync($"/api/v1/auth/invitations/{token}")).StatusCode);

        var auth = await TestScenarios.ReadAsync<AuthResponse>(
            await _guest.PostAsJsonAsync("/api/v1/auth/invitations/accept", new AcceptInvitationRequest(token, "Bukhgalter2026")));
        Assert.Equal("Accountant", auth.User.Role);

        var login = await _guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Bukhgalter2026"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private async Task<(string Email, string Token)> InviteAsync(string role)
    {
        var owner = await factory.CreateAdminClientAsync();
        var email = $"invited-{Guid.NewGuid():N}@sitora.local";
        List<Guid>? buildings = null;
        if (role == "ObjectAdmin")
            buildings = [(await owner.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options))!.Items[0].Id];
        (await owner.PostAsJsonAsync("/api/v1/invitations",
            new CreateInvitationRequest(email, role == "Manager" ? "Приглашённый менеджер" : "Приглашённый сотрудник", role, buildings))).EnsureSuccessStatusCode();
        return (email, factory.Email.LastInviteToken(email)!);
    }
}
