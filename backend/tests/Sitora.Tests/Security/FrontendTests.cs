using System.Net;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Сайт из папки frontend/ отдаёт сам API: страницы без .html в адресе, скрипты и стили.
public class FrontendTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Theory]
    [InlineData("/", "home")]
    [InlineData("/apartments", "apartments")]
    [InlineData("/contact", "contact")]
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
    [InlineData("/js/page.js")]
    [InlineData("/css/styles.css")]
    [InlineData("/favicon.svg")]
    public async Task Asset_IsServed(string path)
    {
        var response = await factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
