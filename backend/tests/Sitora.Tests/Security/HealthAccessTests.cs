using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Sitora.Tests.Security;

// Подробные проверки здоровья (/health/ready, /health) — только изнутри или по ключу; /health/live публичный и без деталей.
public class HealthKeyFactory : LowRateLimitFactory
{
    public const string Key = "health-test-key-0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Health:Key", Key);
    }
}

public class HealthAccessTests(HealthKeyFactory factory) : IClassFixture<HealthKeyFactory>
{
    [Fact]
    public async Task Live_IsPublic_ReadyNeedsInternalNetworkOrKey()
    {
        var outside = factory.CreateClientWithIp("203.0.113.7");

        var live = await outside.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.DoesNotContain("database", await live.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await outside.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outside.GetAsync("/health")).StatusCode);

        using var wrongKey = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        wrongKey.Headers.Add("X-Health-Key", "wrong");
        Assert.Equal(HttpStatusCode.NotFound, (await outside.SendAsync(wrongKey)).StatusCode);

        using var withKey = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        withKey.Headers.Add("X-Health-Key", HealthKeyFactory.Key);
        var ready = await outside.SendAsync(withKey);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("database", await ready.Content.ReadAsStringAsync());

        // Из частной сети (docker, VPS) — без ключа.
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClientWithIp("172.18.0.5").GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_IncludePermissionsPolicy()
    {
        var response = await factory.CreateClientWithIp("203.0.113.8").GetAsync("/health/live");
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()", response.Headers.GetValues("Permissions-Policy").Single());
    }
}
