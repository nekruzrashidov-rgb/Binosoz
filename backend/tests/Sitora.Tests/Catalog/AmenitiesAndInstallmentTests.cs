using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Удобства дома (контент-менеджер) и условия рассрочки застройщика (владелец): сайт показывает только заданное.
public class AmenitiesAndInstallmentTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private async Task<Guid> FirstBuildingAsync()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>(
            "/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        return list!.Items.OrderBy(b => b.Name).First().Id;
    }

    [Fact]
    public async Task Content_SavesAmenities_InKeyOrder_WithoutDuplicates_GuestSeesThem_AuditWritten()
    {
        var id = await FirstBuildingAsync();
        var guest = factory.CreateClient();
        Assert.False((await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).TryGetProperty("amenities", out _));

        var content = await factory.CreateContentClientAsync();
        var saved = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/amenities",
            new { amenities = new[] { "elevators", "playground", "elevators", "underground_parking" } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // Кэш сброшен; порядок — как в BuildingAmenities.Keys, повтор убран.
        var b = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        var keys = b.GetProperty("amenities").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        Assert.Equal(["underground_parking", "playground", "elevators"], keys);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "AmenitiesUpdated")));

        // Пустой список — поля нет.
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{id}/amenities", new { amenities = Array.Empty<string>() })).StatusCode);
        Assert.False((await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).TryGetProperty("amenities", out _));
    }

    [Fact]
    public async Task Amenities_UnknownKey_Is400_WithCode_AndOnlyCatalogEditors()
    {
        var id = await FirstBuildingAsync();
        var content = await factory.CreateContentClientAsync();
        var bad = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/amenities", new { amenities = new[] { "helipad" } });
        Assert.True(bad.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity, bad.StatusCode.ToString());
        Assert.Contains("Неизвестное удобство дома", await bad.Content.ReadAsStringAsync());
        // На таджикском — перевод из ErrorCatalog.
        content.DefaultRequestHeaders.Add("Accept-Language", "tg");
        var badTg = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/amenities", new { amenities = new[] { "helipad" } });
        Assert.Contains("Қулайии номаълуми бино", await badTg.Content.ReadAsStringAsync());
        content.DefaultRequestHeaders.Remove("Accept-Language");

        var body = new { amenities = new[] { BuildingAmenities.Keys[0] } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PutAsJsonAsync($"/api/v1/buildings/{id}/amenities", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).PutAsJsonAsync($"/api/v1/buildings/{id}/amenities", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await content.PutAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}/amenities", body)).StatusCode);
    }

    [Fact]
    public async Task Owner_SavesInstallment_GuestSeesItInBuilding_AuditWritten_EmptyHidesIt()
    {
        var id = await FirstBuildingAsync();
        var guest = factory.CreateClient();
        var admin = await factory.CreateAdminClientAsync();

        var saved = await admin.PutAsJsonAsync("/api/v1/owner/installment-settings", new { downPaymentMinPct = 15m, maxMonths = 60, noOverpayment = true });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var settings = await admin.GetFromJsonAsync<JsonElement>("/api/v1/owner/installment-settings");
        Assert.Equal(15m, settings.GetProperty("downPaymentMinPct").GetDecimal());

        var terms = (await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).GetProperty("installment");
        Assert.Equal(15m, terms.GetProperty("downPaymentMinPct").GetDecimal());
        Assert.Equal(60, terms.GetProperty("maxMonths").GetInt32());
        Assert.True(terms.GetProperty("noOverpayment").GetBoolean());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "InstallmentSettingsUpdated")));

        // Только срок — взноса в ответе нет; ничего не задано — нет и поля.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/owner/installment-settings", new { maxMonths = 24 })).StatusCode);
        var partial = (await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).GetProperty("installment");
        Assert.False(partial.TryGetProperty("downPaymentMinPct", out _));
        Assert.False(partial.GetProperty("noOverpayment").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/v1/owner/installment-settings", new { })).StatusCode);
        Assert.False((await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).TryGetProperty("installment", out _));
    }

    [Theory]
    [InlineData("{\"downPaymentMinPct\":120}", "Первоначальный взнос — от 0 до 100%")]
    [InlineData("{\"maxMonths\":0}", "Срок рассрочки — от 1 до 360 месяцев")]
    [InlineData("{\"maxMonths\":1000}", "Срок рассрочки — от 1 до 360 месяцев")]
    public async Task Installment_Validation_InRussian(string body, string message)
    {
        var admin = await factory.CreateAdminClientAsync();
        var r = await admin.PutAsync("/api/v1/owner/installment-settings", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity, r.StatusCode.ToString());
        Assert.Contains(message, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Installment_OnlyOrganizationLeaders()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).GetAsync("/api/v1/owner/installment-settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateContentClientAsync()).PutAsJsonAsync("/api/v1/owner/installment-settings", new { maxMonths = 12 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/owner/installment-settings")).StatusCode);
    }
}
