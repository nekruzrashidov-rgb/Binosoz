using System.Net;
using System.Net.Http.Json;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// Мелочи контракта API: обязательная организация в каталоге, известные провайдеры webhook.
public class ApiContractTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task Buildings_GuestWithoutOrganization_Is400_UnknownOrganization_Is404()
    {
        var guest = factory.CreateClient();

        var missing = await guest.GetAsync("/api/v1/buildings");
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("Укажите организацию", await missing.Content.ReadAsStringAsync());

        var unknown = await guest.GetAsync("/api/v1/buildings?organization=no-such-developer");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains("Организация не найдена", await unknown.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/v1/buildings?organization=nekruz")).StatusCode);
        // Сотрудник без organization по-прежнему видит дома своей организации.
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync("/api/v1/buildings")).StatusCode);
    }

    [Fact]
    public async Task PaymentWebhook_KnownProviders_501_Unknown_404_WithoutEchoingTheUrl()
    {
        var guest = factory.CreateClient();

        var alif = await guest.PostAsJsonAsync("/api/v1/payment/webhook/alif", new { });
        Assert.Equal(HttpStatusCode.NotImplemented, alif.StatusCode);

        var unknown = await guest.PostAsJsonAsync("/api/v1/payment/webhook/%3Cscript%3Eevil", new { });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.DoesNotContain("evil", await unknown.Content.ReadAsStringAsync());
    }
}
