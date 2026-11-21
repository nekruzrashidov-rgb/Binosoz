using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Security;

// A-12: настройки организации у суперадмина платформы (организации у него нет) — 400 organization_required, а не 500.
// Раньше исключение бросалось внутри LINQ-выражения, EF Core оборачивал его в InvalidOperationException.
public class NoOrganizationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Theory]
    [InlineData("GET", "/api/v1/owner/green-settings")]
    [InlineData("PUT", "/api/v1/owner/green-settings")]
    [InlineData("GET", "/api/v1/owner/installment-settings")]
    [InlineData("PUT", "/api/v1/owner/installment-settings")]
    public async Task SuperAdmin_WithoutOrganization_Gets400(string method, string url)
    {
        var superAdmin = await factory.CreateSuperAdminClientAsync();
        var response = method == "GET"
            ? await superAdmin.GetAsync(url)
            : await superAdmin.PutAsJsonAsync(url, new { isDemo = false, noOverpayment = false });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("organization_required", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("Укажите организацию", json.RootElement.GetProperty("detail").GetString());
    }
}
