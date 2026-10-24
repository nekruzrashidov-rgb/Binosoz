using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Data.Seed;

public static class CatalogSeeder
{
    public const string BuildingName = "Sitora Residence";
    public const int ResidentialFloors = 16;

    // Типы квартир по порядку номеров на этаже: комнат (0 — студия) и площадь.
    public static readonly (int Rooms, decimal Area)[] Layout =
    [
        (1, 46m), (2, 68m), (3, 92m), (2, 70m), (0, 34m), (2, 66m), (1, 48m), (0, 36m)
    ];

    // Фиксированное зерно: при каждом запуске статусы получаются одинаковыми.
    private const int RandomSeed = 20261024;

    public static decimal PricePerM2(int floor) => 9200m + floor * 95m;

    public static int ApartmentNumber(int floor, int position) => floor * 100 + position;

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        if (await db.Buildings.AnyAsync())
            return;

        var random = new Random(RandomSeed);
        var building = new Building
        {
            NameRu = BuildingName,
            NameTg = BuildingName,
            Address = "г. Душанбе",
            FloorsCount = ResidentialFloors,
            // Перевод описания на таджикский не выдумываем: его задаёт контент-менеджер.
            DescriptionRu = "Жилой комплекс с коммерческим первым этажом"
        };

        building.Floors.Add(new Floor { Number = Floor.CommercialFloorNumber });

        for (var floorNumber = 1; floorNumber <= ResidentialFloors; floorNumber++)
        {
            var floor = new Floor { Number = floorNumber };
            // Чем ниже этаж, тем выше доля проданных: от ~75% на 1-м до ~8% на 16-м.
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

            building.Floors.Add(floor);
        }

        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        logger.LogInformation("Создан дом {Building}: {Floors} этажей, {Apartments} квартир",
            BuildingName, building.Floors.Count, building.Floors.Sum(f => f.Apartments.Count));
    }
}
