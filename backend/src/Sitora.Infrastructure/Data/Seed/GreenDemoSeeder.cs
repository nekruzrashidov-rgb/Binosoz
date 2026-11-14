using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Infrastructure.Data.Seed;

// Демо-значения эко-паспорта дома «Некруз Резиденс» и «зелёных» настроек организации по умолчанию.
// Правдоподобные, но не официальные: организация помечается IsDemo — на сайте бейдж «Демо-данные».
// Те же значения записывает backend/scripts/seed.sql (SeedSqlTests сверяет). Коэффициент CO₂ не задаётся:
// проверенного источника для него нет (docs/DECISIONS.md), без него CO₂ на сайте не показывается.
// Ничего не перезаписывает: эко-паспорт — только если его нет, настройки — только если тариф не задан.
public static class GreenDemoSeeder
{
    public static BuildingEcoInfo Eco() => new()
    {
        EnergyClass = EnergyClass.A,
        WallInsulationRu = "Минеральная вата 100 мм",
        WallInsulationTg = "Пахтаи маъданӣ 100 мм",
        WindowsRu = "Двухкамерный стеклопакет",
        WindowsTg = "Шишапакети дукамера",
        HasHeatMeters = true,
        HasWaterMeters = true,
        HasLedCommonAreas = true,
        HasWasteSorting = true,
        GreenAreaM2 = 1200m,
        HeatingType = HeatingType.Central,
        EstimatedKwhPerM2Year = 120m
    };

    public const decimal TariffKwhTjs = 0.29m;
    public const decimal BaselineKwhPerM2Year = 180m;
    public const string BaselineSource = "Демо-значение для показа на конкурсе, не официальные данные";

    public static async Task SeedAsync(AppDbContext db, ILogger logger)
    {
        var building = await db.Buildings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.OrganizationId == Organization.DefaultId && b.NameRu == CatalogSeeder.BuildingName);
        if (building is not null && building.Eco is null)
            building.Eco = Eco();

        var org = await db.Organizations.IgnoreQueryFilters().FirstOrDefaultAsync(o => o.Id == Organization.DefaultId);
        if (org is not null && org.Green.TariffKwhTjs is null)
        {
            org.Green.TariffKwhTjs = TariffKwhTjs;
            org.Green.BaselineKwhPerM2Year = BaselineKwhPerM2Year;
            org.Green.BaselineSource = BaselineSource;
            org.Green.IsDemo = true;
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Демо-данные «зелёной» части: эко-паспорт дома и тарифы организации (помечены как демо)");
        }
    }
}
