using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Seed;

public static class CatalogSeeder
{
    public const string BuildingName = "Некруз Резиденс";
    // Прежнее название дома из сидера: базы разработки, созданные до переименования, получают новое.
    public const string LegacyBuildingName = "Sitora Residence";
    // 20 жилых этажей по 8 квартир = 160 квартир, как в 3D-модели комплекса (nekruz3d.html).
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
    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Buildings.AnyAsync())
        {
            await db.Buildings.Where(b => b.NameRu == LegacyBuildingName)
                .ExecuteUpdateAsync(u => u.SetProperty(b => b.NameRu, BuildingName).SetProperty(b => b.NameTg, b => b.NameTg == LegacyBuildingName ? BuildingName : b.NameTg));
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
            // Перевод описания на таджикский не выдумываем: его задаёт контент-менеджер.
            DescriptionRu = "Жилой комплекс с коммерческим первым этажом"
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
