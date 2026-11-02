using System.Net;
using System.Net.Http.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Infrastructure.Services;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

public class FloorPlanServiceTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    [Fact]
    public async Task SeededPlan_ColorsApartmentsByStatus()
    {
        var manager = await factory.CreateManagerClientAsync();
        var building = await SitoraBuildingAsync();
        var statuses = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.BuildingId == building && a.Floor.Number == FloorPlanSeeder.Floor)
            .ToDictionaryAsync(a => a.Id, a => a.Status));

        Assert.Contains(FloorPlanSeeder.Floor, (await manager.GetFromJsonAsync<List<int>>($"/api/v1/buildings/{building}/floorplan"))!);
        var svg = await GetSvgAsync(manager, building, FloorPlanSeeder.Floor);

        var rects = Apartments(svg);
        Assert.Equal(4, rects.Count);
        foreach (var rect in rects)
        {
            var status = statuses[Guid.Parse((string)rect.Attribute("data-apartment-id")!)];
            Assert.Equal(FloorPlanService.Colors[status], (string?)rect.Attribute("fill"));
            Assert.Equal(status.ToString().ToLowerInvariant(), (string?)rect.Attribute("data-status"));
        }
        Assert.Equal(["#22c55e", "#f59e0b", "#ef4444", "#6b7280"],
            new[] { ApartmentStatus.Free, ApartmentStatus.Booked, ApartmentStatus.Sold, ApartmentStatus.OnRequest }.Select(s => FloorPlanService.Colors[s]));
    }

    [Fact]
    public async Task Rects_GetTitleWithNumberAreaPrice_AndBookingTurnsYellow()
    {
        var owner = await factory.CreateAdminClientAsync();
        var manager = await factory.CreateManagerClientAsync();
        var building = await SitoraBuildingAsync();
        // Свой план на этаже свободной квартиры (не 1-м, там план сидера) — бронируем её и смотрим, как меняется цвет.
        var free = await factory.WithDbAsync(db => db.Apartments
            .Where(a => a.Floor.BuildingId == building && a.Floor.Number > 3 && a.Status == ApartmentStatus.Free)
            .OrderBy(a => a.Number)
            .Select(a => new { a.Id, a.Number, a.Area, a.Price, Floor = a.Floor.Number }).FirstAsync());
        await SaveAsync(owner, building, free.Floor, FloorPlanSeeder.Svg([(free.Id, free.Number)]));

        var rect = Assert.Single(Apartments(await GetSvgAsync(manager, building, free.Floor)));
        var title = rect.Element(Svg + "title")!.Value;
        Assert.StartsWith($"Кв. {free.Number}, {free.Area.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"))} м², ", title);
        Assert.EndsWith(" TJS", title);
        Assert.Equal(FloorPlanService.Colors[ApartmentStatus.Free], (string?)rect.Attribute("fill"));

        Assert.True((await manager.PostAsJsonAsync($"/api/v1/apartments/{free.Id}/bookings",
            new CreateBookingRequest("Клиент плана", SitoraApiFactory.RandomPhone(), null, null))).IsSuccessStatusCode);
        var booked = Assert.Single(Apartments(await GetSvgAsync(manager, building, free.Floor)));
        Assert.Equal((FloorPlanService.Colors[ApartmentStatus.Booked], "booked"), ((string?)booked.Attribute("fill"), (string?)booked.Attribute("data-status")));
        // Подсказка одна, даже если в шаблоне уже был свой <title>.
        Assert.Single(booked.Elements(Svg + "title"));
    }

    [Fact]
    public async Task SoldApartment_CarriesDealId_ForOpeningTheDeal()
    {
        var owner = await factory.CreateAdminClientAsync();
        var deal = await TestScenarios.CreateDealAsync(factory);
        var a = await factory.WithDbAsync(db => db.Deals.Where(d => d.Id == deal.DealId)
            .Select(d => new { d.ApartmentId, d.Apartment.Number, d.Apartment.Floor.BuildingId, Floor = d.Apartment.Floor.Number }).SingleAsync());
        await SaveAsync(owner, a.BuildingId, a.Floor, FloorPlanSeeder.Svg([(a.ApartmentId, a.Number)]));

        var rect = Assert.Single(Apartments(await GetSvgAsync(owner, a.BuildingId, a.Floor)));

        Assert.Equal(("sold", deal.DealId.ToString()), ((string?)rect.Attribute("data-status"), (string?)rect.Attribute("data-deal-id")));
    }

    [Fact]
    public async Task Isolation_ForeignApartmentIsGreyWithoutData_ForeignBuildingIs404_OnlyLeadersSave()
    {
        var owner = await factory.CreateAdminClientAsync();
        var manager = await factory.CreateManagerClientAsync();
        var sitora = await SitoraBuildingAsync();
        var foreign = await factory.WithDbAsync(db => db.Apartments.Where(a => a.Floor.BuildingId == sitora)
            .Select(a => new { a.Id, a.Number }).FirstAsync());
        var other = (await TestScenarios.ReadAsync<BuildingResponse>(await owner.PostAsJsonAsync("/api/v1/buildings",
            new SaveBuildingRequest("План изоляции " + Guid.NewGuid().ToString("N")[..6], "План", "г. Душанбе", 3, null, null)))).Id;
        await TestScenarios.ReadAsync<object>(await owner.PostAsJsonAsync("/api/v1/floors", new CreateFloorRequest(other, 1, null)));

        // В плане другого дома — квартира «Sitora Residence»: ни цены, ни номера, ни статуса оттуда не видно.
        await SaveAsync(owner, other, 1, FloorPlanSeeder.Svg([(foreign.Id, foreign.Number)]));
        var rect = Assert.Single((await GetSvgAsync(manager, other, 1)).Descendants(Svg + "rect"), r => ((string?)r.Attribute("id"))?.StartsWith("apt-") == true);
        Assert.Equal(FloorPlanService.Colors[ApartmentStatus.OnRequest], (string?)rect.Attribute("fill"));
        Assert.Null(rect.Attribute("data-apartment-id"));
        Assert.Empty(rect.Elements(Svg + "title"));

        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/v1/buildings/{Guid.NewGuid()}/floorplan/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/v1/buildings/{other}/floorplan/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync($"/api/v1/buildings/{other}/floorplan/9", new SaveFloorPlanRequest("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PutAsJsonAsync($"/api/v1/buildings/{other}/floorplan/1", new SaveFloorPlanRequest("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync($"/api/v1/buildings/{other}/floorplan/1")).StatusCode);
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.Entity == "FloorPlan" && l.Action == "Updated")));
    }

    [Fact]
    public async Task Save_RejectsScripts_AndStripsHandlersAndLinks()
    {
        var owner = await factory.CreateAdminClientAsync();
        var building = await SitoraBuildingAsync();
        const string ns = "xmlns=\"http://www.w3.org/2000/svg\"";

        foreach (var bad in new[] { $"<svg {ns}><script>alert(1)</script></svg>", $"<svg {ns}><foreignObject><div/></foreignObject></svg>", "<html/>", "не svg", "" })
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await owner.PutAsJsonAsync($"/api/v1/buildings/{building}/floorplan/3", new SaveFloorPlanRequest(bad))).StatusCode);

        var clean = FloorPlanService.Sanitize($"<svg {ns} onload=\"alert(1)\"><g><rect onclick=\"x()\" href=\"javascript:x()\" style=\"fill:url(javascript:x)\" width=\"5\"/></g></svg>");
        Assert.DoesNotContain("onload", clean);
        Assert.DoesNotContain("onclick", clean);
        Assert.DoesNotContain("javascript", clean);
        Assert.Contains("width=\"5\"", clean);
    }

    private Task<Guid> SitoraBuildingAsync() =>
        factory.WithDbAsync(db => db.Buildings.Where(b => b.NameRu == CatalogSeeder.BuildingName).Select(b => b.Id).SingleAsync());

    private static async Task SaveAsync(HttpClient client, Guid building, int floor, string svg) =>
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/buildings/{building}/floorplan/{floor}", new SaveFloorPlanRequest(svg))).StatusCode);

    private static async Task<XElement> GetSvgAsync(HttpClient client, Guid building, int floor)
    {
        var response = await client.GetAsync($"/api/v1/buildings/{building}/floorplan/{floor}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        return XElement.Parse(await response.Content.ReadAsStringAsync());
    }

    private static List<XElement> Apartments(XElement svg) =>
        svg.Descendants(Svg + "rect").Where(r => r.Attribute("data-apartment-id") is not null).ToList();
}
