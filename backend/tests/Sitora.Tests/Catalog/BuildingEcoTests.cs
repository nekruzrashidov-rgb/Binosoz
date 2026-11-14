using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Эко-паспорт дома: правит контент-менеджер и руководители; гость видит в GET дома только заполненные поля.
public class BuildingEcoTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private async Task<Guid> FirstBuildingAsync()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>(
            "/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        return list!.Items.OrderBy(b => b.Name).First().Id;
    }

    private static object FullPassport() => new
    {
        energyClass = "APlus",
        wallInsulationRu = "Минвата 150 мм",
        wallInsulationTg = "Пахтаи маъданӣ 150 мм",
        windowsRu = "Двухкамерный стеклопакет",
        solarPanelsKw = 12.5m,
        hasHeatMeters = true,
        hasLedCommonAreas = false,
        greenAreaM2 = 1800m,
        heatingType = "HeatPump",
        estimatedKwhPerM2Year = 95m
    };

    [Fact]
    public async Task Content_SavesPassport_GuestSeesOnlyFilledFields_CacheReset_AuditWritten()
    {
        var id = await FirstBuildingAsync();
        var guest = factory.CreateClient();
        // В кэше — дом без паспорта.
        var before = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.False(before.TryGetProperty("eco", out _));

        var content = await factory.CreateContentClientAsync();
        var saved = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", FullPassport());
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        // Кэш сброшен: гость сразу видит паспорт; незаполненные поля в JSON не попадают.
        var after = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        var eco = after.GetProperty("eco");
        Assert.Equal("APlus", eco.GetProperty("energyClass").GetString());
        Assert.Equal("Минвата 150 мм", eco.GetProperty("wallInsulation").GetString());
        Assert.Equal(95m, eco.GetProperty("estimatedKwhPerM2Year").GetDecimal());
        Assert.False(eco.GetProperty("hasLedCommonAreas").GetBoolean());
        Assert.False(eco.TryGetProperty("hasWaterMeters", out _));
        Assert.False(eco.TryGetProperty("windowsTg", out _));

        // На таджикском — таджикский текст, а где его нет — русский.
        var tg = factory.CreateClient();
        tg.DefaultRequestHeaders.Add("Accept-Language", "tg");
        var ecoTg = (await tg.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).GetProperty("eco");
        Assert.Equal("Пахтаи маъданӣ 150 мм", ecoTg.GetProperty("wallInsulation").GetString());
        Assert.Equal("Двухкамерный стеклопакет", ecoTg.GetProperty("windows").GetString());

        // И в списке домов каталога.
        var list = await guest.GetFromJsonAsync<JsonElement>("/api/v1/buildings?organization=nekruz&pageSize=100");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), b => b.GetProperty("id").GetGuid() == id && b.TryGetProperty("eco", out _));

        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "EcoUpdated")));

        // Пустой паспорт — поле eco исчезает целиком.
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", new { wallInsulationRu = "  " })).StatusCode);
        var cleared = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.False(cleared.TryGetProperty("eco", out _));
    }

    [Fact]
    public async Task OnlyCatalogEditors_CanEdit()
    {
        var id = await FirstBuildingAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PutAsJsonAsync($"/api/v1/buildings/{id}/eco", FullPassport())).StatusCode);
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", FullPassport())).StatusCode);
        var accountant = await factory.CreateAccountantClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", FullPassport())).StatusCode);
        var admin = await factory.CreateAdminClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", FullPassport())).StatusCode);
    }

    [Theory]
    [InlineData("{\"solarPanelsKw\":-1}", "solarPanelsKw")]
    [InlineData("{\"greenAreaM2\":-5}", "greenAreaM2")]
    [InlineData("{\"estimatedKwhPerM2Year\":5000}", "estimatedKwhPerM2Year")]
    public async Task Validation_RejectsOutOfRange(string body, string field)
    {
        var id = await FirstBuildingAsync();
        var content = await factory.CreateContentClientAsync();
        var response = await content.PutAsync($"/api/v1/buildings/{id}/eco", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity, response.StatusCode.ToString());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(problem.GetProperty("errors").TryGetProperty(field, out _), problem.GetRawText());
    }

    [Fact]
    public async Task UnknownBuilding_Is404()
    {
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await content.PutAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}/eco", FullPassport())).StatusCode);
    }
}
