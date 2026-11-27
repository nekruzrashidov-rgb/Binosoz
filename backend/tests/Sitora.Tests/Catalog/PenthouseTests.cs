using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Пентхаусы на последнем этаже каждого блока (этап 2б): 4 на этаж по 4 комнаты, всего 12, 170–230 м², своя терраса,
// цена = 1 400 × площадь; это квартиры — в счётчике квартир, бронь и продажа как у квартир.
public class PenthouseTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task TopFloors_OnlyPenthouses_4x4Rooms_12Total_PricedAt1400_WithTerrace()
    {
        var blocks = await factory.WithDbAsync(db => db.Buildings.AsNoTracking().Where(b => b.BlockCode != null)
            .Include(b => b.Floors).ThenInclude(f => f.Apartments).ToListAsync());
        var all = new List<Apartment>();
        foreach (var b in blocks)
        {
            var top = b.Floors.Single(f => f.Number == b.FloorsCount).Apartments;
            Assert.Equal(4, top.Count);
            Assert.All(top, a =>
            {
                Assert.Equal(ApartmentKind.Penthouse, a.Kind);
                Assert.Equal(4, a.Rooms);
                Assert.InRange(a.Area, 170m, 230m);
                Assert.Equal(Apartment.CalculatePrice(a.Area, 1400m), a.Price);
                Assert.True(a.TerraceArea > 0, "терраса");
            });
            // Ниже последнего этажа пентхаусов нет.
            Assert.DoesNotContain(b.Floors.Where(f => f.Number < b.FloorsCount).SelectMany(f => f.Apartments), a => a.Kind == ApartmentKind.Penthouse);
            all.AddRange(top);
        }
        Assert.Equal(12, all.Count);
        Assert.Equal([22, 20, 20], blocks.OrderBy(b => b.SortOrder).Select(b => b.Floors.Single(f => f.Apartments.Any(a => a.Kind == ApartmentKind.Penthouse)).Number));
    }

    [Fact]
    public async Task Penthouses_CountAsApartments_AndPublicApiShowsTerrace()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        var a = list!.Items.Single(b => b.BlockCode == "A");
        var counted = await factory.WithDbAsync(db => db.Apartments.CountAsync(x => x.Floor.BuildingId == a.Id && (x.Kind == ApartmentKind.Residential || x.Kind == ApartmentKind.Penthouse)));
        Assert.Equal(counted, a.TotalApartments);
        Assert.Equal(19 * 8 + 4, a.TotalApartments);
        var floors = await factory.CreateClient().GetFromJsonAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{a.Id}/floors?pageSize=100", TestJson.Options);
        var top = await factory.CreateClient().GetFromJsonAsync<PagedResult<ApartmentResponse>>($"/api/v1/floors/{floors!.Items.Single(f => f.Number == 22).Id}/apartments", TestJson.Options);
        Assert.All(top!.Items, x => { Assert.Equal(ApartmentKind.Penthouse, x.Kind); Assert.True(x.TerraceArea > 0); });
    }
}
