using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Точка дома на карте и высота этажа (3D-модель сайта): без координат нет окружения, поэтому по умолчанию полей нет.
public class BuildingLocationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private async Task<Guid> FirstBuildingAsync()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>(
            "/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        return list!.Items.Where(b => b.BlockCode == null).OrderBy(b => b.Name).First().Id;
    }

    [Fact]
    public async Task Content_SavesLocation_GuestSeesIt_AuditWritten_EmptyHidesIt()
    {
        var id = await FirstBuildingAsync();
        var guest = factory.CreateClient();
        var content = await factory.CreateContentClientAsync();
        // Сид координат не выдумывает.
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{id}/location", new { })).StatusCode);
        var empty = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.False(empty.TryGetProperty("latitude", out _));
        Assert.False(empty.TryGetProperty("floorHeight", out _));

        var saved = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/location",
            new { latitude = 38.559812m, longitude = 68.787011m, floorHeight = 3.15m });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var b = await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}");
        Assert.Equal(38.559812m, b.GetProperty("latitude").GetDecimal());
        Assert.Equal(68.787011m, b.GetProperty("longitude").GetDecimal());
        Assert.Equal(3.15m, b.GetProperty("floorHeight").GetDecimal());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == id && a.Action == "LocationUpdated")));

        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{id}/location", new { })).StatusCode);
        Assert.False((await guest.GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{id}")).TryGetProperty("longitude", out _));
    }

    [Theory]
    [InlineData(38.5, null, 3.0, "Укажите широту и долготу вместе")]
    [InlineData(91.0, 68.0, null, "Широта — от -90 до 90")]
    [InlineData(38.5, 181.0, null, "Долгота — от -180 до 180")]
    [InlineData(null, null, 9.0, "Высота этажа — от 2,4 до 6 м")]
    public async Task InvalidLocation_Is400_InRussian(double? lat, double? lon, double? fh, string message)
    {
        var id = await FirstBuildingAsync();
        var content = await factory.CreateContentClientAsync();
        var r = await content.PutAsJsonAsync($"/api/v1/buildings/{id}/location", new { latitude = lat, longitude = lon, floorHeight = fh });
        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity, r.StatusCode.ToString());
        Assert.Contains(message, await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Location_OnlyCatalogEditors_UnknownBuilding404()
    {
        var id = await FirstBuildingAsync();
        var body = new { latitude = 38.5m, longitude = 68.7m };
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().PutAsJsonAsync($"/api/v1/buildings/{id}/location", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateManagerClientAsync()).PutAsJsonAsync($"/api/v1/buildings/{id}/location", body)).StatusCode);
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await content.PutAsJsonAsync($"/api/v1/buildings/{Guid.NewGuid()}/location", body)).StatusCode);
    }
}
