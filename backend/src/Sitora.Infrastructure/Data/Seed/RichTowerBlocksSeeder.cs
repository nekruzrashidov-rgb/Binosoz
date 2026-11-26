using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

// ЖК «Rich Tower» — три блока на одном участке (docs/DECISIONS.md, «Rich Tower: 3 блока»). Это КОНЦЕПТ до проекта
// застройщика: формы, места, этажность и планировки — данные, их меняют в CRM («Каталог» → блок → «Форма и место на
// участке»), сайт везде подписывает «Визуализация, концепт — не проектная документация».
// Блоки стоят буквой «П» вокруг общего двора, разрывы 26 и 28 м: A — Г-образный со скруглённым внешним углом (22 этажа),
// B — вытянутый с эркерами и срезанными торцами (20), C — П-образный со скруглёнными углами, открытый во двор (20).
// Работает в Development и при SEED_RICH_TOWER_BLOCKS=true (Actions → Seed prod); блоки уже есть — ничего не меняет.
public static class RichTowerBlocksSeeder
{
    public const string EnabledVariable = "SEED_RICH_TOWER_BLOCKS";
    public const string ConceptNoteRu = "Концепт: форма, расположение и планировки блока — визуализация до проекта застройщика, не проектная документация.";
    public const string ConceptNoteTg = "Консепсия: шакл, ҷойгиршавӣ ва нақшаҳои блок — визуализатсия то лоиҳаи сохтмончӣ, ҳуҷҷати лоиҳавӣ нест.";
    public const string ConceptNoteEn = "Concept: the block's shape, placement and layouts are a visualisation before the developer's project, not design documentation.";

    public record BlockSpec(string Code, int Floors, int SortOrder, double[][] Footprint, SitePlacement Position, int TopFrom, double[][] Top);

    // Контуры — метры, обход по часовой стрелке на плане (x вправо, y вниз), первая вершина — начало раскладки квартир.
    public static readonly BlockSpec[] Blocks =
    [
        new("A", 22, 1,
            [[10, 0], [38, 0], [38, 18], [19, 18], [19, 34], [0, 34], [0, 0, 6]],
            new SitePlacement { X = -89, Y = -50, RotationDeg = 0 }, 22,
            [[10, 2.5], [38, 2.5], [38, 18], [19, 18], [19, 34], [2.5, 34], [2.5, 2.5, 5]]),
        new("B", 20, 2,
            [[24, 0], [31, 0], [32.5, -1.5], [37.5, -1.5], [39, 0], [46, 0], [50, 4], [50, 16], [46, 20], [39, 20], [37.5, 21.5], [32.5, 21.5],
             [31, 20], [19, 20], [17.5, 21.5], [12.5, 21.5], [11, 20], [4, 20], [0, 16], [0, 4], [4, 0], [11, 0], [12.5, -1.5], [17.5, -1.5], [19, 0]],
            new SitePlacement { X = -25, Y = -60, RotationDeg = 0 }, 20,
            [[24, 1], [46, 1], [49, 4], [49, 16], [46, 19], [4, 19], [1, 16], [1, 4], [4, 1]]),
        new("C", 20, 3,
            [[21, 0], [42, 0, 8], [42, 28], [27, 28], [27, 15], [15, 15], [15, 28], [0, 28], [0, 0, 8]],
            new SitePlacement { X = 81, Y = -55, RotationDeg = 90 }, 20,
            [[21, 1.5], [40.5, 1.5, 7], [40.5, 28], [27, 28], [27, 15], [15, 15], [15, 28], [1.5, 28], [1.5, 1.5, 7]]),
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

    // Типы квартир на этаже по порядку раскладки (комнат: 0 — студия) и площадь — до раскладки по контуру (этап 2г).
    private static readonly (int Rooms, decimal Area)[] Layout = [(1, 46m), (2, 68m), (3, 92m), (2, 70m), (0, 34m), (2, 66m), (1, 48m), (0, 36m)];
    private const int RandomSeed = 20261126;

    public static decimal PricePerM2(int floor) => 9200m + floor * 95m;

    [SuppressMessage("Security", "CA5394", Justification = "Генератор с фиксированным зерном нужен для одинаковых статусов квартир при каждом запуске; к безопасности отношения не имеет.")]
    public static async Task SeedAsync(AppDbContext db, IAuditService audit, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == Organization.DefaultId);
        if (organization is null || await db.Buildings.IgnoreQueryFilters().AnyAsync(b => b.OrganizationId == organization.Id && b.BlockCode != null))
            return;

        var random = new Random(RandomSeed);
        foreach (var spec in Blocks)
        {
            var building = new Building
            {
                OrganizationId = organization.Id,
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
                Setbacks = [new BuildingSetback { FromFloor = spec.TopFrom, Footprint = spec.Top.Select(p => p.ToArray()).ToList() }],
                Amenities = [.. Amenities]
            };
            for (var floorNumber = 1; floorNumber <= spec.Floors; floorNumber++)
            {
                var floor = new Floor { Number = floorNumber };
                var soldProbability = Math.Max(0, 0.6 - (floorNumber - 1) * 0.04);
                for (var slot = 0; slot < Layout.Length; slot++)
                {
                    var (rooms, area) = Layout[slot];
                    var apartment = new Apartment { Number = floorNumber * 100 + slot + 1, Rooms = rooms, PlanSlot = slot };
                    apartment.SetPricing(area, PricePerM2(floorNumber));
                    var roll = random.NextDouble();
                    if (roll < soldProbability)
                        apartment.Sell();
                    else if (roll > 0.97)
                        apartment.SetOnRequest(true);
                    floor.Apartments.Add(apartment);
                }
                building.Floors.Add(floor);
            }
            building.PlannedApartments = building.Floors.Sum(f => f.Apartments.Count);
            db.Buildings.Add(building);
            audit.Record(nameof(Building), building.Id, "Seeded", null, new { building.NameRu, building.BlockCode, building.FloorsCount, Concept = true }, organization.Id);
        }

        organization.SitePlan ??= SitePlan();
        await db.SaveChangesAsync();
        logger.LogInformation("ЖК {Name}: созданы блоки {Blocks} (концепт)", organization.Name, string.Join(", ", Blocks.Select(b => b.Code)));
    }
}
