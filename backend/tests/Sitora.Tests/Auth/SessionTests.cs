using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Auth;
using Sitora.Infrastructure.Security;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// «Запомнить меня»: скользящая сессия refresh-токенов, «Устройства», «Выйти на всех устройствах».
public class SessionTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private const string IPhoneSafari = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";
    private const string WindowsChrome = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    private async Task<(HttpClient Client, AuthResponse Auth)> LoginAsync(string userAgent)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.AccountantEmail, factory.AccountantPassword));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    private static async Task<AuthResponse> RefreshAsync(HttpClient client, string token)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    [Fact]
    public async Task Refresh_ExtendsExpiry_AndKeepsTheSameDevice()
    {
        var (client, login) = await LoginAsync(IPhoneSafari);
        await Task.Delay(20);

        var refreshed = await RefreshAsync(client, login.RefreshToken);

        // Срок скользит: от момента обновления ещё Jwt:RefreshTokenDays (30) дней.
        Assert.True(refreshed.RefreshTokenExpiresAt > login.RefreshTokenExpiresAt);
        Assert.InRange(refreshed.RefreshTokenExpiresAt, DateTime.UtcNow.AddDays(30).AddMinutes(-1), DateTime.UtcNow.AddDays(30).AddMinutes(1));
        var rows = await factory.WithDbAsync(db => db.RefreshTokens
            .Where(t => t.TokenHash == JwtTokenService.HashToken(login.RefreshToken) || t.TokenHash == JwtTokenService.HashToken(refreshed.RefreshToken))
            .ToListAsync());
        Assert.Equal(2, rows.Count);
        Assert.Single(rows.Select(r => r.SessionId).Distinct());
        Assert.Single(rows.Select(r => r.SessionStartedAt).Distinct());
    }

    [Fact]
    public async Task Sessions_ListDevices_MarkCurrent()
    {
        var (phone, _) = await LoginAsync(IPhoneSafari);
        var (pc, _) = await LoginAsync(WindowsChrome);

        var sessions = (await phone.GetFromJsonAsync<List<SessionResponse>>("/api/v1/auth/sessions", TestJson.Options))!;

        var current = Assert.Single(sessions, s => s.Current);
        Assert.Equal("Safari · iPhone", current.Device);
        Assert.Contains(sessions, s => s.Device == "Chrome · Windows" && !s.Current);
        Assert.Equal(sessions[0], current);
        Assert.NotNull(pc);
    }

    [Fact]
    public async Task RevokeSession_ClosesThatDeviceOnly()
    {
        var (phone, phoneAuth) = await LoginAsync(IPhoneSafari);
        var (pc, pcAuth) = await LoginAsync(WindowsChrome);
        var pcSession = (await pc.GetFromJsonAsync<List<SessionResponse>>("/api/v1/auth/sessions", TestJson.Options))!.Single(s => s.Current);

        var revoke = await phone.DeleteAsync($"/api/v1/auth/sessions/{pcSession.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        var pcRefresh = await pc.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(pcAuth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, pcRefresh.StatusCode);
        await RefreshAsync(phone, phoneAuth.RefreshToken);

        var again = await phone.DeleteAsync($"/api/v1/auth/sessions/{pcSession.Id}");
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task RevokeSession_OfAnotherUser_Returns404()
    {
        var (_, _) = await LoginAsync(IPhoneSafari);
        var admin = await factory.CreateAdminClientAsync();
        var adminSession = (await admin.GetFromJsonAsync<List<SessionResponse>>("/api/v1/auth/sessions", TestJson.Options))!.First(s => s.Current);
        var (accountant, _) = await LoginAsync(WindowsChrome);

        var response = await accountant.DeleteAsync($"/api/v1/auth/sessions/{adminSession.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokeAll_SignsOutEveryDevice_AndIsAudited()
    {
        var (phone, phoneAuth) = await LoginAsync(IPhoneSafari);
        var (pc, pcAuth) = await LoginAsync(WindowsChrome);

        var response = await pc.PostAsync("/api/v1/auth/sessions/revoke-all", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<SessionsRevokedResponse>())!.Revoked >= 2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(phoneAuth.RefreshToken))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await pc.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(pcAuth.RefreshToken))).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "SessionsRevokedAll")));
    }

    [Fact]
    public async Task Sessions_WithoutToken_Return401()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/auth/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(IPhoneSafari, "Safari · iPhone")]
    [InlineData(WindowsChrome, "Chrome · Windows")]
    [InlineData("Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Mobile Safari/537.36 YaBrowser/24.10", "Яндекс Браузер · Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7; rv:132.0) Gecko/20100101 Firefox/132.0", "Firefox · macOS")]
    [InlineData("curl/8.0", null)]
    [InlineData(null, null)]
    public void DeviceNames_DescribeUserAgent(string? userAgent, string? expected) =>
        Assert.Equal(expected, DeviceNames.Describe(userAgent));
}
