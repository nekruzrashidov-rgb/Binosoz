using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Магазины на 1–2 этажах, паркинг на −1/−2, 3 лифта в блоке, двор (этап 2в).
public class CommercialParkingTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    private Task<List<Building>> BlocksAsync() => factory.WithDbAsync(db => db.Buildings.AsNoTracking().Where(b => b.BlockCode != null)
        .Include(b => b.Floors).ThenInclude(f => f.Apartments).OrderBy(b => b.SortOrder).ToListAsync());

    [Fact]
    public async Task Floors_Minus2Minus1Parking_1and2Commercial_ApartmentsFrom3_ThreeLifts()
    {
        // Блок C меняет тест пересчёта цен (одна база на класс) — цены сида сверяются по A и B, остальное — по всем.
        foreach (var b in await BlocksAsync())
        {
            var seededPrices = b.BlockCode != "C";
            Assert.Equal(3, b.ElevatorsCount);
            var numbers = b.Floors.Select(f => f.Number).OrderBy(n => n).ToList();
            Assert.Equal([-2, -1], numbers.Take(2));
            Assert.DoesNotContain(0, numbers);
            Assert.Equal(b.FloorsCount, numbers.Max());
            foreach (var f in b.Floors)
            {
                var kinds = f.Apartments.Select(a => a.Kind).Distinct().ToList();
                if (f.Number < 0)
                {
                    Assert.Equal([ApartmentKind.Parking], kinds);
                    Assert.Equal(RichTowerBlocksSeeder.ParkingSpotsPerLevel, f.Apartments.Count);
                    if (seededPrices) Assert.All(f.Apartments, a => Assert.Equal(20000m, a.Price));
                    Assert.All(f.Apartments, a => Assert.InRange(a.Area, 13m, 18m));
                }
                else if (f.Number <= 2)
                {
                    // На 1–2 этаже квартир нет — только магазины 40–250 м² по 1 500 $/м².
                    Assert.Equal([ApartmentKind.Commercial], kinds);
                    Assert.All(f.Apartments, a => Assert.InRange(a.Area, 40m, 250m));
                    if (seededPrices) Assert.All(f.Apartments, a => Assert.Equal(Apartment.CalculatePrice(a.Area, 1500m), a.Price));
                    Assert.All(f.Apartments, a => Assert.Equal(Currency.USD, a.Currency));
                }
                else
                    Assert.DoesNotContain(ApartmentKind.Commercial, kinds);
            }
            Assert.Contains("playground", b.Amenities);
            Assert.Contains("football_field", b.Amenities);
            Assert.Contains("underground_parking", b.Amenities);
        }
    }

    [Fact]
    public async Task PublicApi_KindsAndFloorFlags_CountsWithoutShopsAndSpots()
    {
        var list = await factory.CreateClient().GetFromJsonAsync<PagedResult<BuildingResponse>>("/api/v1/buildings?organization=nekruz&pageSize=100", TestJson.Options);
        var a = list!.Items.Single(b => b.BlockCode == "A");
        Assert.Equal(3, a.ElevatorsCount);
        Assert.Equal(2 * RichTowerBlocksSeeder.ParkingSpotsPerLevel, a.ParkingSpots);
        Assert.Equal(12, a.CommercialUnits);
        var blocks = await BlocksAsync();
        var aDb = blocks.Single(b => b.BlockCode == "A");
        Assert.Equal(aDb.Floors.SelectMany(f => f.Apartments).Count(x => x.Kind is ApartmentKind.Residential or ApartmentKind.Penthouse), a.TotalApartments);

        var floors = await factory.CreateClient().GetFromJsonAsync<PagedResult<FloorResponse>>($"/api/v1/buildings/{a.Id}/floors?pageSize=100", TestJson.Options);
        Assert.All(floors!.Items.Where(f => f.Number < 0), f => Assert.True(f.IsParking));
        Assert.All(floors.Items.Where(f => f.Number is 1 or 2), f => Assert.True(f.IsCommercial));
        Assert.All(floors.Items.Where(f => f.Number >= 3), f => Assert.False(f.IsCommercial || f.IsParking));
        // Фильтры каталога «Коммерция» и «Паркинг» — по виду помещения в ответе API.
        var shops = await factory.CreateClient().GetFromJsonAsync<PagedResult<ApartmentResponse>>($"/api/v1/floors/{floors.Items.Single(f => f.Number == 1).Id}/apartments", TestJson.Options);
        Assert.All(shops!.Items, x => Assert.Equal(ApartmentKind.Commercial, x.Kind));
        var spots = await factory.CreateClient().GetFromJsonAsync<PagedResult<ApartmentResponse>>($"/api/v1/floors/{floors.Items.Single(f => f.Number == -1).Id}/apartments?pageSize=100", TestJson.Options);
        Assert.All(spots!.Items, x => Assert.Equal(ApartmentKind.Parking, x.Kind));
        Assert.Equal(RichTowerBlocksSeeder.ParkingSpotsPerLevel, spots.Items.Count);
    }

    [Fact]
    public async Task ParkingSpot_IsBooked_SecondBookingIs409_InAudit()
    {
        var spotId = await factory.WithDbAsync(db => db.Apartments.Where(a => a.Kind == ApartmentKind.Parking && a.Status == ApartmentStatus.Free)
            .OrderBy(a => a.Number).Select(a => a.Id).FirstAsync());
        var guest = factory.CreateClient();
        var first = await guest.PostAsJsonAsync($"/api/v1/apartments/{spotId}/bookings", new { name = "Гость Паркинга", phone = SitoraApiFactory.RandomPhone() });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var second = await factory.CreateClient().PostAsJsonAsync($"/api/v1/apartments/{spotId}/bookings", new { name = "Другой Гость", phone = SitoraApiFactory.RandomPhone() });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ApartmentStatus.Booked, await factory.WithDbAsync(db => db.Apartments.Where(a => a.Id == spotId).Select(a => a.Status).SingleAsync()));
        Assert.True(await factory.WithDbAsync(db => db.AuditLogs.AnyAsync(l => l.EntityId == spotId)));
    }

    [Fact]
    public async Task ApplyPriceRule_KeepsSpotPriceFixed_ShopsByTheirPrice()
    {
        var b = (await BlocksAsync()).Single(x => x.BlockCode == "C");
        var admin = await factory.CreateAdminClientAsync();
        var rule = await admin.PutAsJsonAsync($"/api/v1/buildings/{b.Id}/price-rule", new
        {
            currency = "USD", steps = b.PriceSteps, penthousePricePerM2 = 1400m, commercialPricePerM2 = 1600m, parkingSpotPrice = 22000m
        });
        Assert.Equal(HttpStatusCode.OK, rule.StatusCode);
        (await admin.PostAsync($"/api/v1/buildings/{b.Id}/price-rule/apply", null)).EnsureSuccessStatusCode();
        var after = (await BlocksAsync()).Single(x => x.BlockCode == "C").Floors.SelectMany(f => f.Apartments).Where(a => a.Status == ApartmentStatus.Free).ToList();
        Assert.All(after.Where(a => a.Kind == ApartmentKind.Parking), a => Assert.Equal(22000m, a.Price));
        Assert.All(after.Where(a => a.Kind == ApartmentKind.Commercial), a => Assert.Equal(Apartment.CalculatePrice(a.Area, 1600m), a.Price));
    }
}
