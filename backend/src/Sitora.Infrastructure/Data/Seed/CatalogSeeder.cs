using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Data.Seed;

public static class CatalogSeeder
{
    // ЖК «Rich Tower» (docs/DECISIONS.md, «Rich Tower»). Прежние названия дома из сидера и seed.sql — базы, созданные до
    // переименования, получают новое; сидеры, которые ищут этот дом (сотрудники, демо-доступ), ищут по всем этим именам.
    public const string BuildingName = "Rich Tower";
    public static readonly string[] LegacyBuildingNames = ["Sitora Residence", "Некруз Резиденс"];
    public static readonly string[] BuildingNames = [BuildingName, .. LegacyBuildingNames];
    public const string OrganizationName = "Rich Tower";
    // Прежние имена организации по умолчанию: «Sitora» (имя проекта, миграция MultiTenancy) и «Некруз Резиденс».
    public static readonly string[] LegacyOrganizationNames = ["Sitora", "Некруз Резиденс"];
    public const string RenamedAction = "Renamed";
    // 20 жилых этажей по 8 квартир = 160 квартир.
    public const int ResidentialFloors = 20;
    // Сколько этажей было в сидере до этого: базы разработки, созданные раньше, получают недостающие этажи.
    private const int PreviousResidentialFloors = 16;

    // Типы квартир по порядку номеров на этаже: комнат (0 — студия) и площадь.
    public static readonly (int Rooms, decimal Area)[] Layout =
    [
        (1, 46m), (2, 68m), (3, 92m), (2, 70m), (0, 34m), (2, 66m), (1, 48m), (0, 36m)
    ];

    // Фиксированное зерно: при каждом запуске статусы получаются одинаковыми.
    private const int RandomSeed = 20261024;

    public static decimal PricePerM2(int floor) => 9200m + floor * 95m;

    public static int ApartmentNumber(int floor, int position) => floor * 100 + position;

    [SuppressMessage("Security", "CA5394", Justification = "Генератор с фиксированным зерном нужен для одинаковых статусов квартир при каждом запуске; к безопасности отношения не имеет.")]
    public static async Task SeedAsync(AppDbContext db, IAuditService audit, ILogger logger)
    {
        await RenameAsync(db, audit, logger);
        if (await db.Buildings.AnyAsync())
        {
            await AddMissingFloorsAsync(db, logger);
            return;
        }

        var random = new Random(RandomSeed);
        var building = new Building
        {
            OrganizationId = Organization.DefaultId,
            NameRu = BuildingName,
            NameTg = BuildingName,
            Address = "г. Душанбе",
            FloorsCount = ResidentialFloors,
            PlannedApartments = ResidentialFloors * Layout.Length,
            // Нейтральное описание без оценок («бизнес-класс» и т. п. — только если их подтвердит застройщик) и его перевод.
            DescriptionRu = "Жилой дом с коммерческим первым этажом",
            DescriptionTg = "Бинои истиқоматӣ бо ошёнаи якуми тиҷоратӣ",
            NameEn = BuildingName,
            DescriptionEn = "A residential building with a commercial ground floor"
        };

        building.Floors.Add(new Floor { Number = Floor.CommercialFloorNumber });
        for (var floorNumber = 1; floorNumber <= ResidentialFloors; floorNumber++)
            building.Floors.Add(NewFloor(floorNumber, random));

        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        logger.LogInformation("Создан дом {Building}: {Floors} этажей, {Apartments} квартир",
            BuildingName, building.Floors.Count, building.Floors.Sum(f => f.Apartments.Count));
    }

    // Жилой этаж: 8 квартир по Layout. Чем ниже этаж, тем выше доля проданных: ~75% на 1-м, на верхних — свободно.
    [SuppressMessage("Security", "CA5394", Justification = "Генератор с фиксированным зерном нужен для одинаковых статусов квартир при каждом запуске; к безопасности отношения не имеет.")]
    private static Floor NewFloor(int floorNumber, Random random)
    {
        var floor = new Floor { Number = floorNumber };
        var soldProbability = 0.75 - (floorNumber - 1) * 0.045;
        for (var position = 1; position <= Layout.Length; position++)
        {
            var (rooms, area) = Layout[position - 1];
            var apartment = new Apartment
            {
                Number = ApartmentNumber(floorNumber, position),
                Rooms = rooms,
                PlanSlot = position - 1
            };
            apartment.SetPricing(area, PricePerM2(floorNumber));

            var roll = random.NextDouble();
            if (roll < soldProbability)
                apartment.Sell();
            else if (roll > 0.97)
                apartment.SetOnRequest(true);

            floor.Apartments.Add(apartment);
        }
        return floor;
    }

    // Прежние имена организации по умолчанию и дома из сидера меняются на «Rich Tower»; имя, заданное владельцем, не
    // трогается. Запись Renamed в AuditLog сохраняется тем же SaveChanges, что и новое имя.
    private static async Task RenameAsync(AppDbContext db, IAuditService audit, ILogger logger)
    {
        var organization = await db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == Organization.DefaultId && LegacyOrganizationNames.Contains(o.Name));
        if (organization is not null)
        {
            audit.Record(nameof(Organization), organization.Id, RenamedAction, new { organization.Name }, new { Name = OrganizationName }, organization.Id);
            logger.LogInformation("Организация «{Old}» переименована в «{New}»", organization.Name, OrganizationName);
            organization.Name = OrganizationName;
        }

        var buildings = await db.Buildings.IgnoreQueryFilters()
            .Where(b => b.OrganizationId == Organization.DefaultId && LegacyBuildingNames.Contains(b.NameRu)).ToListAsync();
        foreach (var building in buildings)
        {
            audit.Record(nameof(Building), building.Id, RenamedAction, new { building.NameRu, building.NameTg, building.NameEn },
                new { NameRu = BuildingName }, building.OrganizationId);
            if (LegacyBuildingNames.Contains(building.NameTg) || building.NameTg == "Некрӯз Резиденс")
                building.NameTg = BuildingName;
            if (building.NameEn is "Nekruz Residence" or "Sitora Residence")
                building.NameEn = BuildingName;
            building.NameRu = BuildingName;
        }

        if (organization is not null || buildings.Count > 0)
            await db.SaveChangesAsync();
    }

    // База разработки с прежними 16 этажами: дом из сидера получает этажи 17–20 и новые числа (20 этажей, 160 квартир).
    // Только дом из сидера (организация по умолчанию, то же название, ровно 16 этажей) — другие дома не трогаются.
    private static async Task AddMissingFloorsAsync(AppDbContext db, ILogger logger)
    {
        var building = await db.Buildings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.OrganizationId == Organization.DefaultId && b.NameRu == BuildingName && b.FloorsCount == PreviousResidentialFloors);
        if (building is null || await db.Floors.IgnoreQueryFilters().AnyAsync(f => f.BuildingId == building.Id && f.Number > PreviousResidentialFloors))
            return;

        // Своё зерно: те же статусы при каждом дозаполнении. Этажи добавляются явно (db.Floors.Add): у новых сущностей
        // ключ уже задан (Guid v7), и через коллекцию дома EF принял бы их за существующие и попытался обновить.
        var random = new Random(RandomSeed + PreviousResidentialFloors);
        for (var floorNumber = PreviousResidentialFloors + 1; floorNumber <= ResidentialFloors; floorNumber++)
        {
            var floor = NewFloor(floorNumber, random);
            floor.BuildingId = building.Id;
            db.Floors.Add(floor);
        }
        building.FloorsCount = ResidentialFloors;
        building.PlannedApartments = ResidentialFloors * Layout.Length;
        await db.SaveChangesAsync();
        logger.LogInformation("Дом {Building}: добавлены этажи {From}–{To}, теперь {Floors} этажей и {Apartments} квартир",
            BuildingName, PreviousResidentialFloors + 1, ResidentialFloors, ResidentialFloors, ResidentialFloors * Layout.Length);
    }
}
