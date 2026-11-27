using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Services;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Platform;

// Карта Google на сайте (feat(map)): точка ЖК задаётся в CRM (валидация, AuditLog), /public/brand отдаёт адрес,
// координаты (свои или центр блоков с координатами) и ключ карты только если он задан. Сид точку не выдумывает.
public class MapLocationTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private Task<JsonElement> BrandAsync(HttpClient c) => c.GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz");

    [Fact]
    public async Task NoPointByDefault_NoKey_ContentSavesPoint_Audited_BadValues400_EmptyRemoves()
    {
        var guest = factory.CreateClient();
        var brand = await BrandAsync(guest);
        Assert.False(brand.TryGetProperty("latitude", out _), "сид точку не выдумывает");
        Assert.False(brand.TryGetProperty("mapsEmbedKey", out _), "без Maps__EmbedKey ключа нет");

        var content = await factory.CreateContentClientAsync();
        var ok = await content.PutAsJsonAsync("/api/v1/owner/brand/location", new { latitude = 38.5751234m, longitude = 68.7811m });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var saved = await ok.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(38.575123m, saved.GetProperty("latitude").GetDecimal());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Entity == "Organization" && a.Action == "LocationUpdated")));

        foreach (var bad in new object[] { new { latitude = 91m, longitude = 10m }, new { latitude = 10m, longitude = -181m }, new { latitude = 10m } })
        {
            var r = await content.PutAsJsonAsync("/api/v1/owner/brand/location", bad);
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        // Менеджеру — нет доступа.
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync("/api/v1/owner/brand/location", new { latitude = 1m, longitude = 1m })).StatusCode);

        var cleared = await content.PutAsJsonAsync("/api/v1/owner/brand/location", new { });
        Assert.Equal(JsonValueKind.Null, (await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("latitude").ValueKind);
    }
}

// Ключ и точка в публичном бренде (свой сервер с Maps__EmbedKey, без кэша чужих тестов).
public class MapsKeyFactory : SitoraApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Maps:EmbedKey", "test-embed-key");
    }
}

public class MapPublicBrandTests(MapsKeyFactory factory) : IClassFixture<MapsKeyFactory>
{
    [Fact]
    public async Task PublicBrand_KeyAndPoint_OwnPointFirst_ElseCentreOfBlocks()
    {
        // Без своей точки — центр опубликованных домов с координатами (два блока).
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        var a = list!.Items.Single(b => b.BlockCode == "A").Id;
        var b = list.Items.Single(x => x.BlockCode == "B").Id;
        var content = await factory.CreateContentClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{a}/location", new { latitude = 38.5m, longitude = 68.7m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{b}/location", new { latitude = 38.6m, longitude = 68.9m })).StatusCode);
        var brand = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz&v=1");
        Assert.Equal("test-embed-key", brand.GetProperty("mapsEmbedKey").GetString());
        Assert.Equal(38.55m, brand.GetProperty("latitude").GetDecimal());
        Assert.Equal(68.8m, brand.GetProperty("longitude").GetDecimal());

        // Своя точка ЖК — важнее центра блоков.
        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync("/api/v1/owner/brand/location", new { latitude = 38.57m, longitude = 68.78m })).StatusCode);
        var own = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz&v=2");
        Assert.Equal(38.57m, own.GetProperty("latitude").GetDecimal());
        Assert.Equal(68.78m, own.GetProperty("longitude").GetDecimal());
    }
}

// «Где находится ЖК?»: адрес из CRM попадает в контекст AI-помощника гостя (EstateContext строится из базы).
public class MapAddressAiTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task OwnerAddress_IsInGuestAiContext()
    {
        const string address = "г. Душанбе, ул. Немата Карабаева, 22";
        await factory.WithDbAsync(async db => { var o = await db.Organizations.SingleAsync(x => x.Id == Organization.DefaultId); o.Address = address; await db.SaveChangesAsync(); });
        using var scope = factory.Services.CreateScope();
        var context = await scope.ServiceProvider.GetRequiredService<EstateContext>().BuildAsync(Organization.DefaultId);
        Assert.Contains("address of the complex and the sales office " + address, context);
    }
}
