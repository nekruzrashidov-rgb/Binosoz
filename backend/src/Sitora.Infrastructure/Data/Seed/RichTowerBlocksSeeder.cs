using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

// ЖК «Rich Tower» — три блока на одном участке (docs/DECISIONS.md, «Rich Tower: 3 блока»). Это КОНЦЕПТ до проекта
// застройщика: формы, места, этажность и планировки — данные, их меняют в CRM («Каталог» → блок → «Форма и место на
// участке»), сайт везде подписывает «Визуализация, концепт — не проектная документация».
// Блоки стоят буквой «П» вокруг общего двора, разрывы 26 и 28 м: A — Г-образный со скруглённым внешним углом (22 этажа),
// B — вытянутый с эркерами и срезанными торцами (20), C — П-образный со скруглёнными углами, открытый во двор (20).
// Работает в Development; на проде те же блоки создаёт backend/scripts/seed.sql (RichTowerBlocksSql). Блоки уже есть — ничего не меняет.
public static class RichTowerBlocksSeeder
{
    public const string ConceptNoteRu = "Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.";
    public const string ConceptNoteTg = "Консепсия: шакл, ҷойгиршавӣ ва нақшаҳои блок — визуализатсия то лоиҳаи сохтмончӣ, ҳуҷҷати лоиҳавӣ нест.";
    public const string ConceptNoteEn = "Concept: the block's shape, placement and layouts are a visualisation before the developer's project, not design documentation.";

    // Unit* — набор квартир типового этажа по порядку раскладки: доли площади полосы и комнаты (0 — студия). Угловые и
    // торцевые (окна на две стороны) — самые большие, 3–4 комнаты; в середине фасадов — студии, 1- и 2-комнатные.
    // Top* — последний этаж, пентхаусы: контур уступа, глубина полосы (м), доли площади 4 пентхаусов (угловые — больше).
    public record BlockSpec(string Code, int Floors, int SortOrder, double[][] Footprint, SitePlacement Position, int TopFrom, double[][] Top,
        double[] UnitWeights, int[] UnitRooms, double TopDepth, double[] TopWeights, int StartSlot, int TopStartSlot);

    // Нумерация (docs/DECISIONS.md): квартиры блока — 1…N подряд с 3-го этажа снизу вверх, на этаже по часовой стрелке
    // от квартиры у лифтового холла (StartSlot — ближайшая к ядру позиция раскладки, TopStartSlot — на этаже пентхаусов);
    // пентхаусы продолжают нумерацию. Магазины — своя нумерация Н-1…, машино-места — М-1… по паркингу блока.
    public static int OrderOnFloor(int slot, int start, int count) => ((slot - start) % count + count) % count;

    // Контуры — метры, обход по часовой стрелке на плане (x вправо, y вниз), первая вершина — начало раскладки квартир.
    public static readonly BlockSpec[] Blocks =
    [
        new("A", 22, 1,
            [[10, 0], [38, 0], [38, 18], [19, 18], [19, 34], [0, 34], [0, 0, 6]],
            new SitePlacement { X = -89, Y = -50, RotationDeg = 0 }, 22,
            [[10, 2.5], [38, 2.5], [38, 18], [19, 18], [19, 34], [2.5, 34], [2.5, 2.5, 5]],
            [0.6, 1.75, 0.8, 1.0, 1.75, 0.6, 0.8, 1.7], [0, 4, 1, 2, 4, 0, 1, 3], 6.5, [1, 1, 1, 1], 4, 1),
        new("B", 20, 2,
            [[24, 0], [31, 0], [32.5, -1.5], [37.5, -1.5], [39, 0], [46, 0], [50, 4], [50, 16], [46, 20], [39, 20], [37.5, 21.5], [32.5, 21.5],
             [31, 20], [19, 20], [17.5, 21.5], [12.5, 21.5], [11, 20], [4, 20], [0, 16], [0, 4], [4, 0], [11, 0], [12.5, -1.5], [17.5, -1.5], [19, 0]],
            new SitePlacement { X = -25, Y = -60, RotationDeg = 0 }, 20,
            [[24, 0], [43, 0], [47, 4], [47, 16], [43, 20], [7, 20], [3, 16], [3, 4], [7, 0]],
            [0.55, 0.85, 1.8, 0.95, 0.55, 0.85, 1.8, 0.95], [0, 1, 4, 2, 0, 1, 4, 2], 8, [1, 1, 1, 1], 5, 2),
        new("C", 20, 3,
            [[21, 0], [42, 0, 8], [42, 28], [27, 28], [27, 15], [15, 15], [15, 28], [0, 28], [0, 0, 8]],
            new SitePlacement { X = 81, Y = -55, RotationDeg = 90 }, 20,
            [[21, 1.5], [40.5, 1.5, 7], [40.5, 28], [27, 28], [27, 15], [15, 15], [15, 28], [1.5, 28], [1.5, 1.5, 7]],
            [0.55, 1.6, 0.8, 1.0, 1.0, 0.8, 1.6, 0.55], [0, 4, 1, 2, 2, 1, 4, 0], 6.5, [0.97, 1.03, 1.03, 0.97], 6, 2),
    ];

    // Участок и двор (концепт): детская площадка и мини-футбольное поле — только потому, что они есть в удобствах блоков.
    public static SitePlan SitePlan() => new()
    {
        Boundary = [[-100, -75], [105, -75], [105, 22], [-100, 22]],
        Zones =
        [
            new SiteZone { Kind = "football_field", Polygon = [[-6, -30], [36, -30], [36, -8], [-6, -8]] },
            new SiteZone { Kind = "playground", Polygon = [[-42, -32], [-24, -32], [-24, -18], [-42, -18]] },
            new SiteZone { Kind = "lawn", Polygon = [[-46, -36], [48, -36], [48, 6], [-46, 6]] },
            new SiteZone { Kind = "path", Polygon = [[-46, 8], [48, 8], [48, 12], [-46, 12]] },
            new SiteZone { Kind = "parking_ramp", Polygon = [[-12, 14], [2, 14], [2, 22], [-12, 22]] },
        ]
    };

    public static readonly string[] Amenities = ["underground_parking", "closed_yard", "playground", "football_field", "elevators", "landscaping", "commercial"];

    private const int RandomSeed = 20261126;

    // Подземный паркинг −1/−2 под каждым блоком, по 28 мест на уровень; 1–2 этажи — 6 магазинов на этаж; 3 лифта в блоке.
    public const int ParkingLevels = 2, ParkingSpotsPerLevel = 28, CommercialFloors = 2, Elevators = 3;
    public const decimal ParkingSpotArea = 13.25m;
    private static readonly double[] ShopWeights = [1.2, 0.8, 1.0, 1.2, 0.8, 1.0];

    [SuppressMessage("Security", "CA5394", Justification = "Генератор с фиксированным зерном нужен для одинаковых статусов квартир при каждом запуске; к безопасности отношения не имеет.")]
    public static async Task SeedAsync(AppDbContext db, IAuditService audit, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == Organization.DefaultId);
        if (organization is null || await db.Buildings.IgnoreQueryFilters().AnyAsync(b => b.OrganizationId == organization.Id && b.BlockCode != null))
            return;

        var random = new Random(RandomSeed);
        foreach (var spec in Blocks)
        {
            var building = NewBlock(spec, organization.Id, random);
            db.Buildings.Add(building);
            audit.Record(nameof(Building), building.Id, "Seeded", null, new { building.NameRu, building.BlockCode, building.FloorsCount, Concept = true }, organization.Id);
        }

        organization.SitePlan ??= SitePlan();
        await db.SaveChangesAsync();
        logger.LogInformation("ЖК {Name}: созданы блоки {Blocks} (концепт)", organization.Name, string.Join(", ", Blocks.Select(b => b.Code)));
    }

    // Блок со всеми этажами и помещениями. random — демо-статусы разработки (часть продана, «под запрос»); null — всё
    // свободно (seed.sql на проде: продаж скрипт не выдумывает).
    [SuppressMessage("Security", "CA5394", Justification = "Генератор с фиксированным зерном нужен для одинаковых статусов квартир при каждом запуске; к безопасности отношения не имеет.")]
    public static Building NewBlock(BlockSpec spec, Guid organizationId, Random? random)
    {
        double Roll() => random?.NextDouble() ?? 0.5;
        bool Sold(double p) => random is not null && Roll() < p;
        {
            var building = new Building
            {
                OrganizationId = organizationId,
                NameRu = "Блок " + spec.Code,
                NameTg = "Блоки " + spec.Code,
                NameEn = "Block " + spec.Code,
                Address = "г. Душанбе, пр. Борбад, 15",
                FloorsCount = spec.Floors,
                DescriptionRu = ConceptNoteRu,
                DescriptionTg = ConceptNoteTg,
                DescriptionEn = ConceptNoteEn,
                BlockCode = spec.Code,
                SortOrder = spec.SortOrder,
                Footprint = spec.Footprint.Select(p => p.ToArray()).ToList(),
                SitePosition = spec.Position,
                Setbacks = [new BuildingSetback { FromFloor = spec.TopFrom, Footprint = spec.Top.Select(p => p.ToArray()).ToList(), PlanDepth = spec.TopDepth }],
                Amenities = [.. Amenities],
                // Цена за м² по этажам в USD (PriceDefaults): 3–6 — 1 200, 7–19 — до 900, выше — 900, пентхаусы — 1 400.
                PriceCurrency = PriceDefaults.Currency,
                PriceSteps = PriceDefaults.Steps(spec.Floors),
                PenthousePricePerM2 = PriceDefaults.PenthousePricePerM2,
                CommercialPricePerM2 = PriceDefaults.CommercialPricePerM2,
                ParkingSpotPrice = PriceDefaults.ParkingSpotPrice,
                ElevatorsCount = Elevators
            };
            // −2 и −1 — подземный паркинг под блоком: машино-места 2,5 × 5,3 м по своей цене.
            for (var level = -ParkingLevels; level <= -1; level++)
            {
                var floor = new Floor { Number = level };
                for (var slot = 0; slot < ParkingSpotsPerLevel; slot++)
                {
                    var spot = new Apartment { Number = (-level - 1) * ParkingSpotsPerLevel + slot + 1, Rooms = 0, PlanSlot = slot, Kind = ApartmentKind.Parking };
                    spot.SetFixedPrice(ParkingSpotArea, PriceDefaults.ParkingSpotPrice, building.PriceCurrency);
                    if (Sold(0.25))
                        spot.Sell();
                    floor.Apartments.Add(spot);
                }
                building.Floors.Add(floor);
            }
            // 1–2 — коммерция: помещения с витринами на улицу вдоль фасада, квартир на них нет.
            var shops = FloorPlanMath.UnitAreas(spec.Footprint, FloorPlanMath.Depth, ShopWeights);
            for (var level = 1; level <= CommercialFloors; level++)
            {
                var floor = new Floor { Number = level };
                for (var slot = 0; slot < shops.Length; slot++)
                {
                    var shop = new Apartment { Number = (level - 1) * shops.Length + slot + 1, Rooms = 0, PlanSlot = slot, Kind = ApartmentKind.Commercial };
                    shop.SetPricing(shops[slot], PriceDefaults.CommercialPricePerM2, building.PriceCurrency);
                    if (Sold(0.2))
                        shop.Sell();
                    floor.Apartments.Add(shop);
                }
                building.Floors.Add(floor);
            }
            // Площади квартир — доли полосы этажа (FloorPlanMath): сумма с коридором и лифтовым узлом = площадь этажа.
            var areas = FloorPlanMath.UnitAreas(spec.Footprint, FloorPlanMath.Depth, spec.UnitWeights);
            var homes = (spec.Floors - CommercialFloors - 1) * areas.Length;
            // Последний этаж — 4 пентхауса по 4 комнаты (гостиная-кухня и 3 спальни) на уступе; терраса — кольцо между контуром
            // блока и контуром уступа, по долям площади; в цену м² терраса не входит.
            {
                var penthouses = FloorPlanMath.UnitAreas(spec.Top, spec.TopDepth, spec.TopWeights);
                var terrace = PlanGeometry.Area(PlanGeometry.Expand(spec.Footprint)) - PlanGeometry.Area(PlanGeometry.Expand(spec.Top));
                var sum = penthouses.Sum();
                var floor = new Floor { Number = spec.Floors };
                for (var slot = 0; slot < penthouses.Length; slot++)
                {
                    var penthouse = new Apartment
                    {
                        Number = homes + OrderOnFloor(slot, spec.TopStartSlot, penthouses.Length) + 1, Rooms = 4, PlanSlot = slot, Kind = ApartmentKind.Penthouse,
                        TerraceArea = Math.Round((decimal)terrace * penthouses[slot] / sum, 1, MidpointRounding.AwayFromZero)
                    };
                    penthouse.SetPricing(penthouses[slot], PriceDefaults.PenthousePricePerM2, building.PriceCurrency);
                    if (Sold(0.15))
                        penthouse.Sell();
                    floor.Apartments.Add(penthouse);
                }
                building.Floors.Add(floor);
            }
            for (var floorNumber = CommercialFloors + 1; floorNumber < spec.Floors; floorNumber++)
            {
                var floor = new Floor { Number = floorNumber };
                var soldProbability = Math.Max(0, 0.6 - (floorNumber - 1) * 0.04);
                for (var slot = 0; slot < areas.Length; slot++)
                {
                    var (rooms, area) = (spec.UnitRooms[slot], areas[slot]);
                    var apartment = new Apartment { Number = (floorNumber - CommercialFloors - 1) * areas.Length + OrderOnFloor(slot, spec.StartSlot, areas.Length) + 1, Rooms = rooms, PlanSlot = slot };
                    apartment.SetPricing(area, PriceRules.PricePerM2(building, floorNumber, floorNumber == spec.Floors)!.Value, building.PriceCurrency);
                    if (random is not null)
                    {
                        var roll = random.NextDouble();
                        if (roll < soldProbability)
                            apartment.Sell();
                        else if (roll > 0.97)
                            apartment.SetOnRequest(true);
                    }
                    floor.Apartments.Add(apartment);
                }
                building.Floors.Add(floor);
            }
            building.PlannedApartments = building.Floors.Sum(f => f.Apartments.Count(a => a.Kind is ApartmentKind.Residential or ApartmentKind.Penthouse));
            return building;
        }
    }
}
