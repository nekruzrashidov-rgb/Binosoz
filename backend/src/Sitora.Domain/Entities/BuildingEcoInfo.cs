using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Эко-паспорт дома: только то, что застройщик знает из проекта. Всё необязательное: незаполненное поле на сайте
// не показывается (ни «—», ни заглушек). Хранится в таблице Buildings (owned-тип, столбцы Eco*).
public class BuildingEcoInfo
{
    public EnergyClass? EnergyClass { get; set; }
    public string? WallInsulationRu { get; set; }
    public string? WallInsulationTg { get; set; }
    public string? WindowsRu { get; set; }
    public string? WindowsTg { get; set; }
    public decimal? SolarPanelsKw { get; set; }
    public bool? HasHeatMeters { get; set; }
    public bool? HasWaterMeters { get; set; }
    public bool? HasLedCommonAreas { get; set; }
    public bool? HasEvCharging { get; set; }
    public bool? HasRainwaterReuse { get; set; }
    public bool? HasWasteSorting { get; set; }
    // Озеленение территории, м².
    public decimal? GreenAreaM2 { get; set; }
    public HeatingType? HeatingType { get; set; }
    // Расчётное потребление энергии по проекту, кВт·ч на м² в год.
    public decimal? EstimatedKwhPerM2Year { get; set; }

    public bool IsEmpty =>
        EnergyClass is null && string.IsNullOrWhiteSpace(WallInsulationRu) && string.IsNullOrWhiteSpace(WallInsulationTg)
        && string.IsNullOrWhiteSpace(WindowsRu) && string.IsNullOrWhiteSpace(WindowsTg) && SolarPanelsKw is null
        && HasHeatMeters is null && HasWaterMeters is null && HasLedCommonAreas is null && HasEvCharging is null
        && HasRainwaterReuse is null && HasWasteSorting is null && GreenAreaM2 is null && HeatingType is null
        && EstimatedKwhPerM2Year is null;
}
