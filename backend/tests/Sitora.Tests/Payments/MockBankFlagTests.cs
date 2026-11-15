using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Sitora.Domain.DTOs.Payments;
using Sitora.Tests.Security;

namespace Sitora.Tests.Payments;

// Тестовый банк вне Development — только с флагом Payments__MockEnabled (показ жюри); без флага его как будто нет.
public class MockBankOnFactory : ProductionFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Payments:MockEnabled", "true");
    }
}

file static class Https
{
    public static HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.sitora.test"),
            AllowAutoRedirect = false
        });
}

public class MockBankOffInProductionTests(ProductionFactory factory) : IClassFixture<ProductionFactory>
{
    [Fact]
    public async Task Production_WithoutFlag_SimulateIs404_StatusSaysDisabled()
    {
        var client = Https.Client(factory);
        var r = await client.PostAsJsonAsync("/api/v1/payment/mock/simulate", new SimulatePaymentRequest("mock_x", true));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.DoesNotContain("платёжном шлюзе", await r.Content.ReadAsStringAsync());

        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/payment/mock/status");
        Assert.False(status.GetProperty("enabled").GetBoolean());
    }
}

public class MockBankOnInProductionTests(MockBankOnFactory factory) : IClassFixture<MockBankOnFactory>
{
    [Fact]
    public async Task Production_WithFlag_BankWorks_UnknownPaymentIs404()
    {
        var client = Https.Client(factory);
        var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/payment/mock/status");
        Assert.True(status.GetProperty("enabled").GetBoolean());

        // The bank answers (not «no such address»): an unknown payment is 404 «not found in the gateway».
        var r = await client.PostAsJsonAsync("/api/v1/payment/mock/simulate", new SimulatePaymentRequest("mock_" + Guid.NewGuid(), true));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("Платёж не найден в платёжном шлюзе", await r.Content.ReadAsStringAsync());
    }
}
