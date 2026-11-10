using System.Net;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Сайт из папки frontend/ отдаёт сам API: страницы без .html в адресе, скрипты и стили.
public class FrontendTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Theory]
    [InlineData("/", "home")]
    [InlineData("/apartments", "apartments")]
    [InlineData("/about", "about")]
    [InlineData("/gallery", "gallery")]
    [InlineData("/contact", "contact")]
    [InlineData("/register", "register")]
    [InlineData("/accept-invite", "accept-invite")]
    [InlineData("/login", "login")]
    [InlineData("/cabinet", "cabinet")]
    [InlineData("/crm", "crm")]
    [InlineData("/platform", "platform")]
    public async Task Page_IsServed_WithSitePolicy(string path, string page)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains($"data-page=\"{page}\"", await response.Content.ReadAsStringAsync());
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' https://cdnjs.cloudflare.com;", policy);
        Assert.Contains("connect-src 'self'", policy);
    }

    [Theory]
    [InlineData("/js/routing-va-navigatsiya.js")]
    [InlineData("/css/styles.css")]
    [InlineData("/favicon.svg")]
    public async Task Asset_IsServed(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownSitePath_IsNotFoundPage()
    {
        var response = await factory.CreateClient().GetAsync("/no-such-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("data-page=\"notfound\"", await response.Content.ReadAsStringAsync());
        Assert.Contains("script-src 'self' https://cdnjs.cloudflare.com;", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    // 3D-модель встраивается только страницами сайта и работает в песочнице без доступа к origin сайта;
    // остальные страницы по-прежнему нельзя встроить никуда.
    [Fact]
    public async Task Model3dPage_IsSandboxed_AndFramedOnlyBySite()
    {
        var client = factory.CreateClient();
        var model = await client.GetAsync("/nekruz3d.html");
        Assert.Equal(HttpStatusCode.OK, model.StatusCode);
        Assert.Equal("SAMEORIGIN", model.Headers.GetValues("X-Frame-Options").Single());
        var policy = model.Headers.GetValues("Content-Security-Policy").Single();
        Assert.StartsWith("sandbox allow-scripts ", policy);
        Assert.DoesNotContain("allow-same-origin", policy);
        Assert.Contains("connect-src 'none'", policy);
        Assert.Contains("frame-ancestors 'self'", policy);

        var site = await client.GetAsync("/apartments");
        Assert.Equal("DENY", site.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", site.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task UnknownApiPath_IsNotFound_NotSitePage()
    {
        var response = await factory.CreateClient().GetAsync("/api/v1/no-such-thing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }
}
