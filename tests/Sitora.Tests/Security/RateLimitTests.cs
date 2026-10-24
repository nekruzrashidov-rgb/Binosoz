using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Sales;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Фабрика с маленькими лимитами. IP клиента задаётся заголовком X-Test-Client-IP,
// чтобы тесты не делили между собой один счётчик.
public class LowRateLimitFactory : SitoraApiFactory
{
    public const string ClientIpHeader = "X-Test-Client-IP";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("RateLimiting:LoginPerMinute", "3");
        builder.UseSetting("RateLimiting:SmsPerHour", "2");
        builder.UseSetting("RateLimiting:PublicFormsPerHour", "2");
        builder.ConfigureTestServices(services => services.AddSingleton<IStartupFilter, TestClientIpFilter>());
    }

    public HttpClient CreateClientWithIp(string ip)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, ip);
        return client;
    }

    private class TestClientIpFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var ip))
                    context.Connection.RemoteIpAddress = ip;
                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

public class RateLimitTests(LowRateLimitFactory factory) : IClassFixture<LowRateLimitFactory>
{
    [Fact]
    public async Task Login_IsLimitedPerIp_With429InRussian()
    {
        var client = factory.CreateClientWithIp("10.0.0.1");
        var request = new LoginRequest("nobody@sitora.local", "Wrong-password-1");

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", request)).StatusCode);

        var limited = await client.PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("Слишком много запросов", await limited.Content.ReadAsStringAsync());
        Assert.True(limited.Headers.RetryAfter is not null);

        // Другой IP считается отдельно.
        var other = await factory.CreateClientWithIp("10.0.0.2").PostAsJsonAsync("/api/v1/auth/login", request);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Fact]
    public async Task SmsSend_IsLimitedPerIp()
    {
        var client = factory.CreateClientWithIp("10.0.1.1");

        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(SitoraApiFactory.RandomPhone()));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/v1/auth/sms/send", new SmsSendRequest(SitoraApiFactory.RandomPhone()));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task PublicForms_LimitGuests_ButNotStaff()
    {
        var guest = factory.CreateClientWithIp("10.0.2.1");
        var lead = new CreateLeadRequest("Гость", "+992901112233", null, null, null);

        Assert.Equal(HttpStatusCode.Created, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await guest.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);

        var manager = factory.CreateClientWithIp("10.0.2.1");
        var login = await manager.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(SitoraApiFactory.ManagerEmail, factory.ManagerPassword));
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        manager.DefaultRequestHeaders.Authorization = new("Bearer", token);
        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/v1/leads", lead)).StatusCode);
    }
}
