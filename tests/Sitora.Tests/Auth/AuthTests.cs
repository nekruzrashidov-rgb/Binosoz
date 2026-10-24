using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.DTOs.Auth;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Auth;

public class AuthTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_Admin_ReturnsJwtWithAdminRole()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(SitoraApiFactory.AdminEmail, factory.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));

        var jwt = new JsonWebToken(auth.AccessToken);
        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
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
        Assert.Equal(["Admin"], me?.Roles);
    }

    [Fact]
    public async Task Health_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
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
}
