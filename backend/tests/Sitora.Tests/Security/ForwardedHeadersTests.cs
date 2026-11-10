using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sitora.WebApi.Extensions;

namespace Sitora.Tests.Security;

// Заголовки обратного прокси (ForwardedHeaders:Enabled=true): Caddy в docker-compose.prod.yml, Render.
public class ForwardedHeadersTests
{
    // Как у Render: несколько адресов в X-Forwarded-For и одно значение в X-Forwarded-Proto.
    // Запрос должен стать HTTPS, иначе UseHttpsRedirection бесконечно шлёт 307 на тот же адрес.
    [Fact]
    public async Task ProxyChain_WithSingleProto_MakesRequestHttps_AndTakesNearestClientIp()
    {
        var context = await RunAsync("203.0.113.7, 10.0.0.5", "https");

        Assert.Equal("https", context.Request.Scheme);
        Assert.True(context.Request.IsHttps);
        Assert.Equal(IPAddress.Parse("10.0.0.5"), context.Connection.RemoteIpAddress);
    }

    [Fact]
    public async Task SingleProxy_StillWorks()
    {
        var context = await RunAsync("198.51.100.20", "https");

        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal(IPAddress.Parse("198.51.100.20"), context.Connection.RemoteIpAddress);
    }

    private static async Task<HttpContext> RunAsync(string forwardedFor, string forwardedProto)
    {
        var services = new ServiceCollection().AddSitoraForwardedHeaders().BuildServiceProvider();
        var options = services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, options);

        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.10.10.10");
        context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        context.Request.Headers["X-Forwarded-Proto"] = forwardedProto;
        await middleware.Invoke(context);
        return context;
    }
}
