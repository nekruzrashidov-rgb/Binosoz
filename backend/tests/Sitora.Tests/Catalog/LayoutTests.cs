using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Этап 4: планировки-многоугольники. Планировки застройщика (JSON сайта) — проверка геометрии и площади, журнал,
// публичный список; угловые, эркерные и пентхаусные квартиры блоков — со своим LayoutCode; площади сходятся с этажом.
public class LayoutTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    // Г-образная квартира 6 вершин: гостиная-кухня (многоугольник), спальня, санузел.
    private static object CornerL() => new
    {
        outline = new[] { new[] { 0.0, 0 }, [10, 0], [10, 6], [5, 6], [5, 10], [0, 10] },
        rooms = new object[]
        {
            new object[] { "living", new[] { new[] { 0.0, 0 }, [10, 0], [10, 6], [5, 6], [5, 4], [0, 4] } },
            new object[] { "bedroom", new[] { new[] { 0.0, 4 }, [5, 4], [5, 8], [0, 8] } },
            new object[] { "bath", new[] { new[] { 0.0, 8 }, [5, 8], [5, 10], [0, 10] } },
        },
    };

    private static async Task<string?> CodeAsync(HttpResponseMessage r) =>
        (await r.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var c) ? c.GetString() : null;

    [Fact]
    public async Task Content_SavesPolygonLayout_Audited_PublicSeesIt_BadGeometryRejected()
    {
        var content = await factory.CreateContentClientAsync();
        var saved = await content.PutAsJsonAsync("/api/v1/layouts/My-L", new { name = "Угловая Г", layout = CornerL() });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("my-l", body.GetProperty("code").GetString());
        Assert.Equal(6, body.GetProperty("layout").GetProperty("outline").GetArrayLength());

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/nekruz/layouts");
        Assert.Contains(list.EnumerateArray(), l => l.GetProperty("code").GetString() == "my-l");
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Entity == "Layout" && a.Action == "Created")));

        // Самопересечение контура, комната вне контура, слишком маленькая площадь — 400 со своим кодом.
        var bow = new { outline = new[] { new[] { 0.0, 0 }, [10, 10], [10, 0], [0, 10] }, rooms = new object[] { new object[] { "living", new[] { new[] { 1.0, 1 }, [2, 1], [2, 2] } } } };
        Assert.Equal("invalid_layout_outline", await CodeAsync(await content.PutAsJsonAsync("/api/v1/layouts/bow", new { layout = bow })));
        var outside = new { outline = new[] { new[] { 0.0, 0 }, [6, 0], [6, 6], [0, 6] }, rooms = new object[] { new object[] { "living", new[] { new[] { 0.0, 0 }, [8, 0], [8, 6], [0, 6] } } } };
        Assert.Equal("layout_room_outside", await CodeAsync(await content.PutAsJsonAsync("/api/v1/layouts/out", new { layout = outside })));
        var tiny = new { outline = new[] { new[] { 0.0, 0 }, [3, 0], [3, 3], [0, 3] }, rooms = new object[] { new object[] { "living", new[] { new[] { 0.0, 0 }, [3, 0], [3, 3], [0, 3] } } } };
        Assert.Equal("invalid_layout_area", await CodeAsync(await content.PutAsJsonAsync("/api/v1/layouts/tiny", new { layout = tiny })));
        // Чужие роли — нет доступа.
        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync("/api/v1/layouts/x", new { layout = CornerL() })).StatusCode);
    }

    [Fact]
    public async Task Blocks_CornerBayPenthouse_HaveLayoutCodes_InPublicApi()
    {
        var a = RichTowerBlocksSeeder.NewBlock(RichTowerBlocksSeeder.Blocks.Single(b => b.Code == "A"), Guid.NewGuid(), null);
        var homes = a.Floors.SelectMany(f => f.Apartments).Where(x => x.Kind is ApartmentKind.Residential).ToList();
        Assert.Contains(homes, x => x.LayoutCode == "corner-l" && x.Rooms == 4);
        Assert.All(a.Floors.SelectMany(f => f.Apartments).Where(x => x.Kind == ApartmentKind.Penthouse), x => Assert.Equal("ph-4", x.LayoutCode));

        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        var c = list!.Items.Single(b => b.BlockCode == "C");
        var floors = await factory.CreateClient().GetFromJsonAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{c.Id}/floors?pageSize=100", TestJson.Options);
        var apts = await factory.CreateClient().GetFromJsonAsync<PagedResult<ApartmentResponse>>($"/api/v1/floors/{floors!.Items.Single(f => f.Number == 5).Id}/apartments", TestJson.Options);
        Assert.Contains(apts!.Items, x => x.LayoutCode == "corner-round");
    }

    // Сверка: квартиры типового этажа + коридор + ядро с 3 лифтами = площадь этажа; у пентхаусов — по контуру уступа.
    [Fact]
    public void FloorAreas_AddUp_ForEveryBlock()
    {
        foreach (var spec in RichTowerBlocksSeeder.Blocks)
        {
            var floor = PlanGeometry.Area(PlanGeometry.Expand(spec.Footprint));
            var band = FloorPlanMath.BandArea(spec.Footprint, FloorPlanMath.Depth);
            var units = (double)FloorPlanMath.UnitAreas(spec.Footprint, FloorPlanMath.Depth, spec.UnitWeights).Sum();
            Assert.InRange(units, band * 0.97, band + 1); // площади квартир округлены до 0,1 м²
            Assert.True(floor > band, spec.Code + ": внутри полосы — коридор и ядро");
            var top = (double)FloorPlanMath.UnitAreas(spec.Top, spec.TopDepth, spec.TopWeights).Sum();
            Assert.InRange(top, FloorPlanMath.BandArea(spec.Top, spec.TopDepth) * 0.97, FloorPlanMath.BandArea(spec.Top, spec.TopDepth) + 1);
        }
    }
}
