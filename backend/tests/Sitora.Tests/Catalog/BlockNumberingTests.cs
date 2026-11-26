using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data.Seed;
using Sitora.Tests.Infrastructure;

namespace Sitora.Tests.Catalog;

// Нумерация блоков ЖК (docs/DECISIONS.md): квартиры 1…N подряд с 3-го этажа снизу вверх, на этаже по часовой стрелке
// от квартиры у лифтового холла; пентхаусы — последние номера; магазины Н-1…, машино-места М-1… — свои серии.
public class BlockNumberingTests
{
    private static List<(int Floor, Apartment Unit)> Units(Building b, params ApartmentKind[] kinds) =>
        b.Floors.SelectMany(f => f.Apartments.Select(a => (Floor: f.Number, Unit: a)))
            .Where(x => kinds.Contains(x.Unit.Kind)).OrderBy(x => x.Unit.Number).ToList();

    [Fact]
    public void Homes_1ToN_WithoutGapsOrRepeats_From3rdFloorUp_ClockwiseFromLiftHall_PenthousesLast()
    {
        foreach (var spec in RichTowerBlocksSeeder.Blocks)
        {
            var block = RichTowerBlocksSeeder.NewBlock(spec, Guid.NewGuid(), null);
            var homes = Units(block, ApartmentKind.Residential, ApartmentKind.Penthouse);

            Assert.Equal(Enumerable.Range(1, homes.Count), homes.Select(x => x.Unit.Number));
            Assert.Equal(3, homes[0].Floor);
            Assert.True(homes.Zip(homes.Skip(1)).All(p => p.First.Floor <= p.Second.Floor), $"{spec.Code}: снизу вверх");

            // На этаже — от квартиры у лифтового холла дальше по раскладке (контур обходится по часовой стрелке).
            var perFloor = spec.UnitWeights.Length;
            foreach (var floor in homes.Where(x => x.Unit.Kind == ApartmentKind.Residential).GroupBy(x => x.Floor))
                Assert.Equal(Enumerable.Range(0, perFloor).Select(i => (spec.StartSlot + i) % perFloor), floor.Select(x => x.Unit.PlanSlot));

            var penthouses = homes.Where(x => x.Unit.Kind == ApartmentKind.Penthouse).ToList();
            Assert.Equal(4, penthouses.Count);
            Assert.All(penthouses, p => Assert.Equal(spec.Floors, p.Floor));
            Assert.Equal(Enumerable.Range(homes.Count - 3, 4), penthouses.Select(p => p.Unit.Number));
            Assert.Equal(spec.TopStartSlot, penthouses[0].Unit.PlanSlot);
        }

        var a = RichTowerBlocksSeeder.NewBlock(RichTowerBlocksSeeder.Blocks.Single(b => b.Code == "A"), Guid.NewGuid(), null);
        Assert.Equal([153, 154, 155, 156], Units(a, ApartmentKind.Penthouse).Select(x => x.Unit.Number));
    }

    [Fact]
    public void ShopsAndParking_OwnSeries_NotInApartmentNumbering()
    {
        foreach (var spec in RichTowerBlocksSeeder.Blocks)
        {
            var block = RichTowerBlocksSeeder.NewBlock(spec, Guid.NewGuid(), null);
            var shops = Units(block, ApartmentKind.Commercial);
            var spots = Units(block, ApartmentKind.Parking);
            Assert.Equal(Enumerable.Range(1, shops.Count), shops.Select(x => x.Unit.Number));
            Assert.Equal(Enumerable.Range(1, spots.Count), spots.Select(x => x.Unit.Number));
            Assert.All(shops, x => Assert.InRange(x.Floor, 1, 2));
            Assert.All(spots, x => Assert.InRange(x.Floor, -2, -1));
            // −1 — первые места (въезд), −2 — дальше.
            Assert.Equal(-1, spots[0].Floor);
            // Число квартир не зависит от магазинов и машино-мест.
            Assert.Equal(block.PlannedApartments, Units(block, ApartmentKind.Residential, ApartmentKind.Penthouse).Count);
        }
    }

    [Theory]
    [InlineData("A", ApartmentKind.Residential, 45, "ru", "Блок A, кв. 45", "A-45")]
    [InlineData("A", ApartmentKind.Commercial, 3, "ru", "Блок A, пом. Н-3", "A-Н-3")]
    [InlineData("B", ApartmentKind.Parking, 12, "ru", "Блок B, м/м М-12", "B-М-12")]
    [InlineData("A", ApartmentKind.Penthouse, 153, "en", "Block A, apt. 153", "A-153")]
    [InlineData("C", ApartmentKind.Residential, 7, "tg", "Блоки C, хонаи 7", "C-7")]
    [InlineData(null, ApartmentKind.Residential, 1201, "ru", "кв. №1201", "1201")]
    public void Names_BlockAndNumber(string? block, ApartmentKind kind, int number, string lang, string full, string shortName)
    {
        Assert.Equal(full, UnitNames.Full(block, kind, number, lang));
        Assert.Equal(shortName, UnitNames.Short(block, kind, number));
        Assert.Equal(shortName, new UnitRef(number, block, kind).ToString());
    }

    [Fact]
    public void InText_KeepsOldHouseFormat_AndNamesBlock()
    {
        Assert.Equal("кв. №501", ((UnitRef)501).InText());
        Assert.Equal("кв. 45 в блоке A", new UnitRef(45, "A").InText());
        Assert.Equal("apt. 45 in Block A", new UnitRef(45, "A").InText("en"));
        Assert.Equal("хонаи 45 дар блоки A", new UnitRef(45, "A").InText("tg"));
    }

    private static string MockFile()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, "frontend", "tools", "e2e", "tests", "complex-mock.js");
            if (File.Exists(path))
                return path;
        }
        throw new FileNotFoundException("frontend/tools/e2e/tests/complex-mock.js не найден");
    }

    // Позиция у лифтового холла (StartSlot) — та же, что у макета e2e: там тест сверяет её с геометрией этажа (S.geom.floor).
    [Fact]
    public void StartSlots_SameAsE2eMock()
    {
        var mock = File.ReadAllText(MockFile());
        foreach (var spec in RichTowerBlocksSeeder.Blocks)
        {
            var m = Regex.Match(mock, $@"code: '{spec.Code}'[\s\S]*?start: (\d+), topStart: (\d+)");
            Assert.True(m.Success, spec.Code);
            Assert.Equal(spec.StartSlot, int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(spec.TopStartSlot, int.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}

// Уникальность номера в пределах дома — в базе (индекс BuildingId + Number, своя серия у каждого вида помещений).
public class BlockNumberUniquenessTests(SitoraApiFactory factory) : IClassFixture<SitoraApiFactory>
{
    [Fact]
    public async Task SameNumberOnAnotherFloorOfBlock_409_OtherBlockOrOtherSeries_Ok_BuildingIdFromFloor()
    {
        var (blockA, floor4A, blockB, floor4B) = await factory.WithDbAsync(async db =>
        {
            var a = await db.Buildings.AsNoTracking().SingleAsync(b => b.BlockCode == "A");
            var b = await db.Buildings.AsNoTracking().SingleAsync(x => x.BlockCode == "B");
            return (a.Id, await db.Floors.Where(f => f.BuildingId == a.Id && f.Number == 4).Select(f => f.Id).SingleAsync(),
                b.Id, await db.Floors.Where(f => f.BuildingId == b.Id && f.Number == 4).Select(f => f.Id).SingleAsync());
        });
        var client = await factory.CreateContentClientAsync();

        // Кв. 1 блока A — на 3-м этаже; тот же номер на 4-м этаже — 409.
        var taken = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor4A, 1, 1, 40m, 1000m, 50));
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        var problem = await taken.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("apartment_number_taken", problem.GetProperty("code").GetString());

        // Новый номер — можно; дом квартиры ставит база по этажу.
        var created = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor4A, 9001, 1, 40m, 1000m, 50));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<ApartmentResponse>(TestJson.Options);
        Assert.Equal("A", body!.BlockCode);
        Assert.Equal(blockA, await factory.WithDbAsync(db => db.Apartments.Where(x => x.Id == body.Id).Select(x => x.BuildingId).SingleAsync()));

        // Тот же номер в другом блоке — можно.
        var otherBlock = await client.PostAsJsonAsync("/api/v1/apartments", new CreateApartmentRequest(floor4B, 9001, 1, 40m, 1000m, 50));
        Assert.Equal(HttpStatusCode.Created, otherBlock.StatusCode);
        Assert.NotEqual(blockA, blockB);

        // Магазин Н-1 и кв. 1 в одном блоке не мешают друг другу: в базе обе с номером 1.
        var ones = await factory.WithDbAsync(db => db.Apartments.Where(x => x.BuildingId == blockA && x.Number == 1).Select(x => x.Kind).ToListAsync());
        Assert.Contains(ApartmentKind.Residential, ones);
        Assert.Contains(ApartmentKind.Commercial, ones);
        Assert.Contains(ApartmentKind.Parking, ones);
    }
}
