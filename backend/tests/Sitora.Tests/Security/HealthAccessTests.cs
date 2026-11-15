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

        // Снаружи без ключа — только общий статус, без списка проверок.
        foreach (var path in new[] { "/health/ready", "/health" })
        {
            var brief = await outside.GetAsync(path);
            var body = await brief.Content.ReadAsStringAsync();
            Assert.Contains("\"status\"", body);
            Assert.DoesNotContain("checks", body);
            Assert.DoesNotContain("database", body);
        }

        using var wrongKey = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        wrongKey.Headers.Add("X-Health-Key", "wrong");
        Assert.DoesNotContain("checks", await (await outside.SendAsync(wrongKey)).Content.ReadAsStringAsync());

        using var withKey = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        withKey.Headers.Add("X-Health-Key", HealthKeyFactory.Key);
        var ready = await outside.SendAsync(withKey);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Contains("database", await ready.Content.ReadAsStringAsync());

        // Вне Production из частной сети (docker) — полный ответ и без ключа.
        Assert.Contains("database", await (await factory.CreateClientWithIp("172.18.0.5").GetAsync("/health/ready")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SecurityHeaders_IncludePermissionsPolicy()
    {
        var response = await factory.CreateClientWithIp("203.0.113.8").GetAsync("/health/live");
        Assert.Equal("camera=(), microphone=(), geolocation=(), payment=()", response.Headers.GetValues("Permissions-Policy").Single());
    }
}

// Production: внутренний адрес не в счёт (на Render все запросы идут через прокси) — без ключа только статус, с ключом — всё.
public class HealthProductionFactory : ProductionFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Health:Key", HealthKeyFactory.Key);
    }
}

public class HealthProductionTests(HealthProductionFactory factory) : IClassFixture<HealthProductionFactory>
{
    private HttpClient Https() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://api.sitora.test"),
        AllowAutoRedirect = false
    });

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    public async Task WithoutKey_OnlyStatus_EvenFromInside(string path)
    {
        // TestServer — «внутренний» адрес; в Production это не даёт подробностей.
        var body = await (await Https().GetAsync(path)).Content.ReadAsStringAsync();
        Assert.Contains("\"status\"", body);
        Assert.DoesNotContain("checks", body);

        using var wrong = new HttpRequestMessage(HttpMethod.Get, path);
        wrong.Headers.Add("X-Health-Key", "not-the-key");
        Assert.DoesNotContain("checks", await (await Https().SendAsync(wrong)).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task WithKey_FullReport_LiveStaysPublicWithVersion()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/ready");
        request.Headers.Add("X-Health-Key", HealthKeyFactory.Key);
        var full = await (await Https().SendAsync(request)).Content.ReadAsStringAsync();
        Assert.Contains("checks", full);
        Assert.Contains("database", full);

        var live = await (await Https().GetAsync("/health/live")).Content.ReadAsStringAsync();
        Assert.Contains("version", live);
        Assert.DoesNotContain("checks", live);
    }
}
