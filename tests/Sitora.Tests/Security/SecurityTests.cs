using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

public class SecurityTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Responses_HaveSecurityHeaders_AndNoServerHeader()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/buildings");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.False(response.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Cors_AllowsOnlyConfiguredOrigins()
    {
        var client = factory.CreateClient();

        var allowed = await client.SendAsync(Preflight(SitoraApiFactory.AllowedOrigin));
        var denied = await client.SendAsync(Preflight("https://evil.example"));

        Assert.Equal(SitoraApiFactory.AllowedOrigin, allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task BlockedUser_LosesLoginAndSessions_AndAdminCannotBlockSelf()
    {
        var admin = await factory.CreateAdminClientAsync();
        var email = $"blocked-{Guid.NewGuid():N}@sitora.local";
        const string password = "Blocked-User-1";
        var user = await TestScenarios.ReadAsync<UserResponse>(await admin.PostAsJsonAsync("/api/v1/users",
            new CreateUserRequest(email, "Уволенный менеджер", "Manager", password, null)));
        var guest = factory.CreateClient();
        var session = await TestScenarios.ReadAsync<AuthResponse>(await guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password)));

        var blocked = await admin.PatchAsJsonAsync($"/api/v1/users/{user.Id}/status", new SetUserStatusRequest(false));
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(session.RefreshToken))).StatusCode);

        var adminId = await factory.WithDbAsync(db => Task.FromResult(db.Users.Single(u => u.Email == SitoraApiFactory.AdminEmail).Id));
        var selfBlock = await admin.PatchAsJsonAsync($"/api/v1/users/{adminId}/status", new SetUserStatusRequest(false));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, selfBlock.StatusCode);

        await admin.PatchAsJsonAsync($"/api/v1/users/{user.Id}/status", new SetUserStatusRequest(true));
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password))).StatusCode);
    }

    [Fact]
    public async Task Errors_DoNotLeakInternals()
    {
        var response = await factory.CreateClient().GetAsync($"/api/v1/apartments/{Guid.NewGuid()}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain("at Sitora", body);
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/leads");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return request;
    }
}

public class ProductionFactory : SitoraApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Production");
        // В Production API сам миграции не применяет; тесту нужна готовая база.
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }
}

public class ProductionTests(ProductionFactory factory) : IClassFixture<ProductionFactory>
{
    [Fact]
    public async Task Production_RequiresHttps_SendsHsts_AndHidesSwagger()
    {
        // HSTS не отправляется для localhost, поэтому запросы идут на «настоящий» домен.
        var http = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://api.sitora.test"),
            AllowAutoRedirect = false
        });
        var https = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.sitora.test"),
            AllowAutoRedirect = false
        });

        var redirect = await http.GetAsync("/api/v1/buildings");
        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirect.StatusCode);
        Assert.Equal("https", redirect.Headers.Location?.Scheme);

        var secure = await https.GetAsync("/api/v1/buildings");
        Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
        Assert.Contains("max-age=31536000", secure.Headers.GetValues("Strict-Transport-Security").Single());

        Assert.Equal(HttpStatusCode.NotFound, (await https.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await https.GetAsync("/")).StatusCode);
    }
}

// Окружение Staging: сидер разработки не работает, первый админ создаётся из BOOTSTRAP_ADMIN_*.
public class StagingFactory : SitoraApiFactory
{
    public const string BootstrapEmail = "owner@sitora.local";
    public string BootstrapPassword { get; } = "Owner-" + Guid.NewGuid().ToString("N")[..10] + "A1";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Staging");
        builder.UseSetting("BOOTSTRAP_ADMIN_EMAIL", BootstrapEmail);
        builder.UseSetting("BOOTSTRAP_ADMIN_PASSWORD", BootstrapPassword);
    }
}

public class StagingTests(StagingFactory factory) : IClassFixture<StagingFactory>
{
    [Fact]
    public async Task OutsideDevelopment_NoSeedData_AndBootstrapAdminCanLogIn()
    {
        var client = factory.CreateClient();

        var buildings = await client.GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings", TestJson.Options);
        Assert.Equal(0, buildings!.TotalCount);

        var seededAdmin = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.AdminEmail, factory.AdminPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, seededAdmin.StatusCode);

        var owner = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(StagingFactory.BootstrapEmail, factory.BootstrapPassword));
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
        var auth = await owner.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(["Admin"], auth!.User.Roles);
    }
}
