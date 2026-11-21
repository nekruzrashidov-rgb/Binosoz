using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// A-10: английские название, описание и тексты эко-паспорта сохраняются и отдаются публичным API (nameEn, descriptionEn,
// eco.wallInsulationEn, eco.windowsEn); не заданы — полей нет, сайт показывает русский текст с пометкой.
public class BuildingEnglishTextsTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task EnglishTexts_AreSaved_AndShownToGuests_EmptyAreOmitted()
    {
        var admin = await factory.CreateAdminClientAsync();
        var name = "Английский дом " + Guid.NewGuid().ToString("N")[..6];
        var created = await admin.PostAsJsonAsync("/api/v1/buildings", new SaveBuildingRequest(name, name, "г. Душанбе", 3, "Жилой дом", null));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var guest = factory.CreateClient();
        var plain = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.False(plain.TryGetProperty("descriptionEn", out _));
        Assert.False(plain.TryGetProperty("nameEn", out _));

        var saved = await admin.PutAsJsonAsync($"/api/v1/buildings/{id}", new SaveBuildingRequest(name, name, "г. Душанбе", 3, "Жилой дом", null,
            NameEn: "English house", DescriptionEn: "A residential building"));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var eco = await admin.PutAsJsonAsync($"/api/v1/buildings/{id}/eco", new SaveBuildingEcoRequest(
            WallInsulationRu: "Минвата 100 мм", WallInsulationEn: "Mineral wool, 100 mm", WindowsEn: "Double glazing"));
        Assert.Equal(HttpStatusCode.OK, eco.StatusCode);

        var after = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.Equal("English house", after.GetProperty("nameEn").GetString());
        Assert.Equal("A residential building", after.GetProperty("descriptionEn").GetString());
        Assert.Equal("Mineral wool, 100 mm", after.GetProperty("eco").GetProperty("wallInsulationEn").GetString());
        Assert.Equal("Double glazing", after.GetProperty("eco").GetProperty("windowsEn").GetString());
        // Русские тексты на месте: запасной язык сайта.
        Assert.Equal("Жилой дом", after.GetProperty("descriptionRu").GetString());
    }
}
