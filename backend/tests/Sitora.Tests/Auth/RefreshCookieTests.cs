using System.Net;
using System.Net.Http.Json;
using Sitora.Domain.DTOs.Auth;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

// Двойной режим refresh-токена: вход ставит HttpOnly-cookie sitora_rt и по-прежнему отдаёт токен в теле;
// /auth/refresh принимает токен и из тела, и из cookie (тело можно не присылать); выход удаляет cookie.
public class RefreshCookieTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Login_SetsHttpOnlyCookie_AndKeepsTokenInBody()
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        login.EnsureSuccessStatusCode();

        var cookie = login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("sitora_rt=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", cookie, StringComparison.OrdinalIgnoreCase);
        var body = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(body!.RefreshToken));
    }

    [Fact]
    public async Task Refresh_WorksFromCookie_AndFromBody()
    {
        // Клиент тестового сервера хранит cookie сам, как браузер.
        var browser = factory.CreateClient();
        var login = await browser.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        var first = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;

        // Из cookie: тела нет вовсе.
        var viaCookie = await browser.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, viaCookie.StatusCode);
        var second = (await viaCookie.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // Из тела, без cookie (как сейчас делает сайт на другом домене).
        var app = factory.CreateClient(new() { HandleCookies = false });
        var viaBody = await app.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(second.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, viaBody.StatusCode);

        // Ни тела, ни cookie — 400 с русским текстом.
        var none = await app.PostAsync("/api/v1/auth/refresh", null);
        Assert.Equal(HttpStatusCode.BadRequest, none.StatusCode);
        Assert.Contains("refresh-токен", await none.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Logout_RevokesTokenFromCookie_AndClearsIt()
    {
        var browser = factory.CreateClient();
        var login = await browser.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.RefreshToken;

        var logout = await browser.PostAsync("/api/v1/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("sitora_rt=;", StringComparison.Ordinal));

        var reuse = await factory.CreateClient(new() { HandleCookies = false }).PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token));
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }
}
