using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Integrations;
using Sitora.Tests.Infrastructure;
using Sitora.Tests.Security;

namespace Sitora.Tests.Auth;

// GET /auth/methods и честный отказ вместо «код отправлен», когда письмо или SMS с кодом никуда не уйдёт.
public class LoginChannelsDevelopmentTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Development_EmailAndSmsWork()
    {
        // В Development коды видны в логе: оба способа считаются рабочими (SMS включён флагом в фабрике тестов).
        var methods = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.True(methods.GetProperty("email").GetBoolean());
        Assert.True(methods.GetProperty("sms").GetBoolean());
    }
}

// Staging без почтового провайдера (Email:Provider по умолчанию Dev) и с заглушкой SMS — как Render до настройки.
// Общая фабрика тестов подменяет SMS поддельным отправителем; здесь возвращается настоящая заглушка.
public class StubSmsStagingFactory : StagingFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISmsSender>();
            services.AddSingleton<ISmsSender, SmsSender>();
        });
    }
}

public class LoginChannelsNotConfiguredTests(StubSmsStagingFactory factory) : IClassFixture<StubSmsStagingFactory>
{
    [Fact]
    public async Task Methods_ReportNothingWorks()
    {
        var methods = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/auth/methods");
        Assert.False(methods.GetProperty("email").GetBoolean());
        Assert.False(methods.GetProperty("sms").GetBoolean());
    }

    [Theory]
    [InlineData("client1@example.com")]
    [InlineData("nobody-at-all@example.com")]
    public async Task EmailSend_Is503_ForAnyAddress(string address)
    {
        // Одинаково для существующей и несуществующей почты: по ответу нельзя узнать, есть ли покупатель.
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/email/send", new { email = address });
        await AssertProblemAsync(response, "email_not_configured", "Письма с кодами не отправляются: почтовый сервис не настроен");
    }

    [Fact]
    public async Task Register_Is503_BeforeCreatingCustomer()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register",
            new { email = "new-buyer@example.com", firstName = "Тест", lastName = "Покупатель", phone = "+992900001234" });
        await AssertProblemAsync(response, "email_not_configured", "Письма с кодами не отправляются: почтовый сервис не настроен");
    }

    [Fact]
    public async Task SmsSend_Is503_WithoutProvider()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/sms/send", new { phone = "+992900000001" });
        await AssertProblemAsync(response, "sms_not_configured", "SMS с кодами пока не отправляются. Войдите по почте");
    }

    [Fact]
    public async Task Tajik_TextFromCatalog()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept-Language", "tg");
        var response = await client.PostAsJsonAsync("/api/v1/auth/email/send", new { email = "client1@example.com" });
        await AssertProblemAsync(response, "email_not_configured", "Мактубҳо бо код фиристода намешаванд: хизматрасонии почта танзим нашудааст");
    }

    [Fact]
    public async Task Ready_EmailDegraded()
    {
        // Из TestServer запрос «изнутри»: /health/ready отвечает подробно.
        var ready = await factory.CreateClient().GetFromJsonAsync<HealthBody>("/health/ready", TestJson.Options);
        Assert.Equal("Degraded", ready!.Checks.Single(c => c.Name == "email").Status);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, string code, string detail)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.ServiceUnavailable, $"{(int)response.StatusCode}: {body}");
        var root = JsonDocument.Parse(body).RootElement;
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal(detail, root.GetProperty("detail").GetString());
    }
}
