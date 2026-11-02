using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Вход: сотрудники — по паролю (с 2FA, если включена), покупатели — по коду из почты.
public class AuthTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_Owner_ReturnsJwtWithOwnerRole()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(SitoraApiFactory.AdminEmail, factory.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Owner");
        Assert.Equal(auth.User.Id.ToString(), jwt.Subject);

        var lifetime = jwt.ValidTo - jwt.IssuedAt;
        Assert.Equal(TimeSpan.FromMinutes(15), lifetime);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithRussianMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(SitoraApiFactory.ManagerEmail, "Wrong-password-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Неверная почта или пароль", problem?.Detail);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest("nobody@sitora.local", "Some-password-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_EmptyEmail_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("", "x"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Укажите почту", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndOldTokenIsRejected()
    {
        var login = await LoginAdminAsync();

        var refreshed = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var newTokens = await refreshed.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEqual(login.RefreshToken, newTokens!.RefreshToken);

        var reused = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);

        // После попытки повторного использования вся цепочка сессий отозвана.
        var afterReuse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(newTokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var login = await LoginAdminAsync();

        var logout = await _client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithToken_ReturnsCurrentUser()
    {
        var login = await LoginAdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(SitoraApiFactory.AdminEmail, me?.Email);
        Assert.Equal(["Owner"], me?.Roles);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Healthy\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_DescribesAuthEndpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/v1/auth/login", await response.Content.ReadAsStringAsync());
    }

    private async Task<AuthResponse> LoginAdminAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(SitoraApiFactory.AdminEmail, factory.AdminPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task Login_StaffWithPassword_GetsTokens_AndItIsAudited()
    {
        var auth = await LoginOk(SitoraApiFactory.ManagerEmail, factory.ManagerPassword);

        Assert.Equal("Manager", auth.User.Role);
        Assert.Equal(Organization.DefaultId, auth.User.OrganizationId);
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == auth.User.Id && l.Action == "Login")));
    }

    [Fact]
    public async Task Login_Customer_Gets400_UseCodeLogin()
    {
        var email = $"buyer-{Guid.NewGuid():N}@mail.test";
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Покупатель" };
            Assert.True((await users.CreateAsync(user, "Buyer-Pass-1")).Succeeded);
            await users.AddToRoleAsync(user, RoleNames.Customer);
        });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Buyer-Pass-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Используйте вход по коду", (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LockFor15Minutes_EvenWithTheRightOne()
    {
        var (email, password) = await NewStaffAsync();

        for (var i = 0; i < 5; i++)
        {
            var wrong = await Login(email, "Wrong-pass-1");
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal("Неверная почта или пароль", (await wrong.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
        }

        // Верный пароль во время блокировки не помогает.
        var locked = await Login(email, password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Contains("Слишком много неудачных попыток", (await locked.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
        var lockoutEnd = await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.LockoutEnd).SingleAsync());
        Assert.InRange(lockoutEnd!.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15.5));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.CountAsync(l => l.Action == "LoginFailed"
            && db.Users.Any(u => u.Id == l.EntityId && u.Email == email))) >= 5);

        // После срока блокировки вход снова работает.
        await factory.WithDbAsync(db => db.Users.Where(u => u.Email == email)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, DateTimeOffset.UtcNow.AddSeconds(-1))));
        Assert.Equal(HttpStatusCode.OK, (await Login(email, password)).StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_AnswersLikeWrongPassword()
    {
        var response = await Login($"ghost-{Guid.NewGuid():N}@nowhere.test", "Any-pass-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Неверная почта или пароль", (await response.Content.ReadFromJsonAsync<ProblemDetails>())?.Detail);
    }

    [Fact]
    public async Task Login_WithTwoFactor_ReturnsTempToken_ThenCodeGivesTokens()
    {
        var (email, password) = await NewStaffAsync();
        var key = await EnableTotpAsync(email);

        var first = await Login(email, password);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadAsStringAsync();
        Assert.DoesNotContain("accessToken", body);
        var twoFactor = (await first.Content.ReadFromJsonAsync<TwoFactorRequiredResponse>(TestJson.Options))!;
        Assert.True(twoFactor.RequiresTwoFactor);

        // Неверный код и чужой токен не подходят.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify2Fa(twoFactor.TempToken, "000000" == Totp.Code(key) ? "111111" : "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify2Fa("not-a-token", Totp.Code(key))).StatusCode);

        var auth = await TestScenarios.ReadAsync<AuthResponse>(await Verify2Fa(twoFactor.TempToken, Totp.Code(key)));
        Assert.Equal("Manager", auth.User.Role);

        // Временный токен одноразовый.
        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify2Fa(twoFactor.TempToken, Totp.Code(key))).StatusCode);
    }

    [Fact]
    public async Task TwoFactor_FiveWrongCodes_BurnTheTempToken()
    {
        var (email, password) = await NewStaffAsync();
        var key = await EnableTotpAsync(email);
        var twoFactor = (await (await Login(email, password)).Content.ReadFromJsonAsync<TwoFactorRequiredResponse>(TestJson.Options))!;
        var wrong = Totp.Code(key) == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Verify2Fa(twoFactor.TempToken, wrong)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await Verify2Fa(twoFactor.TempToken, Totp.Code(key))).StatusCode);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

    private async Task<AuthResponse> LoginOk(string email, string password) =>
        await TestScenarios.ReadAsync<AuthResponse>(await Login(email, password));

    private Task<HttpResponseMessage> Verify2Fa(string tempToken, string code) =>
        _client.PostAsJsonAsync("/api/v1/auth/2fa/verify", new TwoFactorVerifyRequest(tempToken, code));

    // Свой сотрудник на каждый тест: блокировки и 2FA не мешают остальным.
    private async Task<(string Email, string Password)> NewStaffAsync()
    {
        var email = $"staff-{Guid.NewGuid():N}@sitora.test";
        const string password = "Staff-Pass-1";
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = "Сотрудник", OrganizationId = Organization.DefaultId };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            await users.AddToRoleAsync(user, RoleNames.Manager);
        });
        return (email, password);
    }

    private async Task<string> EnableTotpAsync(string email)
    {
        string key = "";
        await factory.WithServiceAsync<UserManager<AppUser>>(async users =>
        {
            var user = (await users.FindByEmailAsync(email))!;
            await users.ResetAuthenticatorKeyAsync(user);
            key = (await users.GetAuthenticatorKeyAsync(user))!;
            await users.SetTwoFactorEnabledAsync(user, true);
        });
        return key;
    }
}

// Код приложения-аутентификатора (RFC 6238: HMAC-SHA1, шаг 30 секунд, 6 цифр) — как у Identity.
public static class Totp
{
    public static string Code(string base32Key, DateTimeOffset? at = null)
    {
        var step = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counter);
        // SHA-1 здесь — часть стандарта TOTP (так считают Google/Microsoft Authenticator), а не выбор шифрования.
#pragma warning disable CA5350
        var hash = HMACSHA1.HashData(Base32(base32Key), counter);
#pragma warning restore CA5350
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] Base32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in input.TrimEnd('=').ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal))
        {
            value = (value << 5) | alphabet.IndexOf(c, StringComparison.Ordinal);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }
        return output.ToArray();
    }
}
