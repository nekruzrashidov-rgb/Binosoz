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
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains($"data-page=\"{page}\"", html);
        // CSP script-src 'self' блокирует встроенный код: каждый <script> — только с src.
        Assert.DoesNotMatch(@"<script(?![^>]*\ssrc=)[^>]*>", html);
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("script-src 'self' https://cdnjs.cloudflare.com;", policy);
        Assert.Contains("connect-src 'self'", policy);
    }

    [Theory]
    [InlineData("/js/routing-va-navigatsiya.js")]
    [InlineData("/css/styles.css")]
    [InlineData("/favicon.svg")]
    [InlineData("/sw.js")]
    [InlineData("/js/pwa-sabt.js")]
    [InlineData("/offline.html")]
    [InlineData("/robots.txt")]
    [InlineData("/sitemap.xml")]
    [InlineData("/js/i18n/ru.js")]
    [InlineData("/js/i18n/tg.js")]
    [InlineData("/js/i18n/en.js")]
    [InlineData("/js/tanzimot-api-url.js")]
    public async Task Asset_IsServed(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // PWA: манифест отдаётся как JSON, страницы ссылаются на него и регистрируют Service Worker своим скриптом (без inline).
    [Fact]
    public async Task Pwa_ManifestServed_AndLinkedFromPages()
    {
        var client = factory.CreateClient();
        var manifest = await client.GetAsync("/manifest.json");
        Assert.Equal(HttpStatusCode.OK, manifest.StatusCode);
        Assert.Equal("application/json", manifest.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"start_url\": \"/\"", await manifest.Content.ReadAsStringAsync());

        var page = await (await client.GetAsync("/crm")).Content.ReadAsStringAsync();
        Assert.Contains("<link rel=\"manifest\" href=\"/manifest.json\">", page);
        Assert.Contains("<script src=\"/js/pwa-sabt.js\" defer></script>", page);
        // Every script of the page is deferred: nothing blocks the first paint.
        Assert.DoesNotMatch(@"<script src=""[^""]+""></script>", page);
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

    // Страницы сайта нельзя встроить никуда; отдельной 3D-модели в песочнице (nekruz3d.html, Babylon.js) больше нет.
    [Fact]
    public async Task SitePages_CannotBeFramed_AndOld3dModelIsGone()
    {
        var client = factory.CreateClient();
        var site = await client.GetAsync("/apartments");
        Assert.Equal("DENY", site.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", site.Headers.GetValues("Content-Security-Policy").Single());

        var model = await client.GetAsync("/nekruz3d.html");
        Assert.Equal(HttpStatusCode.NotFound, model.StatusCode);
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
