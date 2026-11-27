using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// ЖК «Rich Tower» — три блока на одном участке: контур, место, уступы (PUT /buildings/{id}/shape), план участка, сид.
public class BlockShapeTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private async Task<List<BuildingResponse>> BlocksAsync()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>(
            "/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        return list!.Items.Where(b => b.BlockCode is not null).OrderBy(b => b.SortOrder).ToList();
    }

    private static IReadOnlyList<PlanGeometry.P> Placed(BuildingResponse b) =>
        PlanGeometry.Place(PlanGeometry.Expand(b.Footprint!), b.SitePosition!.X, b.SitePosition.Y, b.SitePosition.RotationDeg);

    // Код ошибки: у ошибок сервиса — поле code; у ошибок валидации (code = validation_failed) — код текста detail из
    // ErrorCatalog (тот же текст переводится на tg/en).
    private static async Task<(string Code, string Detail)> ErrorAsync(HttpResponseMessage r)
    {
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var code = body.GetProperty("code").GetString()!;
        var detail = body.GetProperty("detail").GetString()!;
        if (code == "validation_failed" && Sitora.WebApi.Errors.ErrorCatalog.TryFind(detail, out var entry, out _))
            code = entry.Code;
        return (code, detail);
    }

    private static async Task<string> CodeAsync(HttpResponseMessage r) => (await ErrorAsync(r)).Code;

    [Fact]
    public async Task Seed_Creates3Blocks_22_20_20_NotRectangles_AtLeast20mApart_WithSitePlan()
    {
        var blocks = await BlocksAsync();
        Assert.Equal(["A", "B", "C"], blocks.Select(b => b.BlockCode));
        Assert.Equal([22, 20, 20], blocks.Select(b => b.FloorsCount));
        foreach (var b in blocks)
        {
            // Формы — не прямоугольники: больше 4 вершин или скругления.
            Assert.True(b.Footprint!.Count > 4 || b.Footprint.Any(p => p.Length > 2), b.BlockCode);
            Assert.True(PlanGeometry.IsSimple(PlanGeometry.Vertices(b.Footprint)), b.BlockCode);
            // Уступ — на последнем этаже (пентхаусы), внутри контура.
            var top = Assert.Single(b.Setbacks!);
            Assert.Equal(b.FloorsCount, top.FromFloor);
            Assert.True(PlanGeometry.Inside(PlanGeometry.Expand(b.Footprint), PlanGeometry.Expand(top.Footprint), 0.05));
            Assert.Contains("концепт", b.Description, StringComparison.OrdinalIgnoreCase);
        }
        var gaps = new[] { (0, 1), (1, 2), (0, 2) }.Select(p => PlanGeometry.Distance(Placed(blocks[p.Item1]), Placed(blocks[p.Item2]))).ToList();
        Assert.All(gaps, g => Assert.True(g >= 20, $"разрыв {g:0.0} м"));
        // «Бок о бок»: соседние блоки — с разрывом 24–30 м.
        Assert.InRange(gaps[0], 24, 30);
        Assert.InRange(gaps[1], 24, 30);

        var brand = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz");
        var zones = brand.GetProperty("sitePlan").GetProperty("zones").EnumerateArray().Select(z => z.GetProperty("kind").GetString()).ToList();
        Assert.Contains("playground", zones);
        Assert.Contains("football_field", zones);
        // Площадка и поле на плане — только потому, что они есть в удобствах.
        Assert.All(blocks, b => Assert.Contains("football_field", b.Amenities!));
        Assert.All(blocks, b => Assert.Contains("playground", b.Amenities!));
    }

    [Theory]
    [InlineData("[[0,0],[10,0]]", "polygon_vertex_count")]
    [InlineData("[[0,0],[10,10],[10,0],[0,10]]", "polygon_self_intersection")]
    [InlineData("[[0,0],[5,0],[10,0]]", "polygon_zero_area")]
    [InlineData("[[0,0],[10,0],[10,10],[0,10,99]]", "polygon_invalid_point")]
    public async Task InvalidFootprint_Is400_WithCode(string footprint, string code)
    {
        var block = (await BlocksAsync())[2];
        var content = await factory.CreateContentClientAsync();
        var r = await content.PutAsJsonAsync($"/api/v1/buildings/{block.Id}/shape", new
        {
            blockCode = "C", footprint = JsonSerializer.Deserialize<double[][]>(footprint), sitePosition = block.SitePosition
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Equal(code, await CodeAsync(r));
    }

    [Fact]
    public async Task OverlappingOrTooCloseBlocks_Are400_SetbackOutside400_ManagerForbidden()
    {
        var blocks = await BlocksAsync();
        var b = blocks[1];
        var content = await factory.CreateContentClientAsync();
        object Shape(double x, double y, List<BuildingSetback>? setbacks = null) =>
            new { blockCode = "B", sortOrder = 2, footprint = b.Footprint, sitePosition = new { x, y, rotationDeg = 0 }, setbacks = setbacks ?? b.Setbacks };

        // На месте блока A — пересечение.
        var overlap = await content.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/shape", Shape(-80, -45));
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Equal("blocks_overlap", await CodeAsync(overlap));
        // В 10 м от блока A — меньше 20 м.
        var close = await content.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/shape", Shape(-41, -50));
        Assert.Equal(HttpStatusCode.BadRequest, close.StatusCode);
        var (closeCode, closeDetail) = await ErrorAsync(close);
        Assert.Equal("blocks_too_close", closeCode);
        Assert.Contains("10 м", closeDetail);
        // Уступ больше самого блока.
        var outside = await content.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/shape",
            Shape(b.SitePosition!.X, b.SitePosition.Y, [new BuildingSetback { FromFloor = 20, Footprint = [[-5, -5], [60, -5], [60, 30], [-5, 30]] }]));
        Assert.Equal(HttpStatusCode.BadRequest, outside.StatusCode);
        Assert.Equal("setback_outside_block", await CodeAsync(outside));
        // Буква занята другим блоком.
        var taken = await content.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/shape", new { blockCode = "A", footprint = b.Footprint, sitePosition = b.SitePosition });
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);

        var manager = await factory.CreateManagerClientAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/shape", Shape(b.SitePosition.X, b.SitePosition.Y))).StatusCode);
        // Ничего не сохранилось.
        Assert.Equal(b.SitePosition.X, (await BlocksAsync())[1].SitePosition!.X);
    }

    [Fact]
    public async Task ShapeSaved_InAudit_GuestSeesIt_EmptyFootprintMakesPlainBuilding()
    {
        var c = (await BlocksAsync())[2];
        var content = await factory.CreateContentClientAsync();
        var moved = await content.PutAsJsonAsync($"/api/v1/buildings/{c.Id}/shape", new
        {
            blockCode = "c", sortOrder = 7, footprint = c.Footprint, sitePosition = new { x = c.SitePosition!.X + 5, y = c.SitePosition.Y, rotationDeg = c.SitePosition.RotationDeg },
            setbacks = c.Setbacks
        });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var guest = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{c.Id}");
        Assert.Equal("C", guest.GetProperty("blockCode").GetString());
        Assert.Equal(7, guest.GetProperty("sortOrder").GetInt32());
        Assert.Equal(c.SitePosition.X + 5, guest.GetProperty("sitePosition").GetProperty("x").GetDouble());
        Assert.True(guest.GetProperty("footprint").GetArrayLength() > 4);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.EntityId == c.Id && a.Action == "ShapeUpdated")));

        Assert.Equal(HttpStatusCode.OK, (await content.PutAsJsonAsync($"/api/v1/buildings/{c.Id}/shape", new { blockCode = "C" })).StatusCode);
        var plain = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/buildings/{c.Id}");
        Assert.False(plain.TryGetProperty("footprint", out _));
        Assert.False(plain.TryGetProperty("sitePosition", out _));
    }

    [Fact]
    public async Task SitePlan_LeadersSave_UnknownZone400_InAudit()
    {
        var admin = await factory.CreateAdminClientAsync();
        var bad = await admin.PutAsJsonAsync("/api/v1/owner/site-plan", new
        {
            boundary = new[] { new[] { 0d, 0 }, [100, 0], [100, 80], [0, 80] }, zones = new[] { new { kind = "pool", polygon = new[] { new[] { 1d, 1 }, [5, 1], [5, 5] } } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("unknown_site_zone", await CodeAsync(bad));
        var ok = await admin.PutAsJsonAsync("/api/v1/owner/site-plan", new
        {
            boundary = new[] { new[] { 0d, 0 }, [100, 0], [100, 80], [0, 80] }, zones = new[] { new { kind = "playground", polygon = new[] { new[] { 1d, 1 }, [15, 1], [15, 12], [1, 12] } } }
        });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var brand = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/public/brand?slug=nekruz");
        Assert.Equal(4, brand.GetProperty("sitePlan").GetProperty("boundary").GetArrayLength());
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(a => a.Action == "SitePlanUpdated")));
        Assert.Equal(HttpStatusCode.Forbidden, (await (await factory.CreateContentClientAsync()).PutAsJsonAsync("/api/v1/owner/site-plan", new { })).StatusCode);
    }

    [Fact]
    public void Geometry_ShoelaceArea_RoundedCorner_Offset()
    {
        var square = PlanGeometry.Vertices([[0, 0], [10, 0], [10, 10], [0, 10]]);
        Assert.Equal(100, PlanGeometry.Area(square), 6);
        // Скруглённый угол r = 2 срезает r²(1 − π/4) ≈ 0,858 м².
        var rounded = PlanGeometry.Expand([[0, 0, 2], [10, 0], [10, 10], [0, 10]]);
        Assert.Equal(100 - 4 * (1 - Math.PI / 4), PlanGeometry.Area(rounded), 1);
        // Сдвиг внутрь на 2 м: квадрат 6 × 6.
        Assert.Equal(36, PlanGeometry.Area(PlanGeometry.OffsetInward(square, 2)), 6);
        Assert.False(PlanGeometry.IsSimple(PlanGeometry.Vertices([[0, 0], [10, 10], [10, 0], [0, 10]])));
    }
}
