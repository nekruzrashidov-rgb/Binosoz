namespace Sitora.Domain.Common;

// Ориентировочные расходы на энергию квартиры по проектным данным дома:
//   кВт·ч в год = площадь × расчётное потребление дома (кВт·ч/м² в год);
//   стоимость в год = кВт·ч в год × тариф (TJS/кВт·ч), до дирамов (2 знака, половина — от нуля, как в остальном коде);
//   в месяц — годовые значения / 12 с тем же округлением.
// Сравнение с «типовым домом» — только если задано его потребление: те же формулы, разница = типовой − этот дом.
public static class UtilityCalculator
{
    public record Estimate(
        decimal KwhPerYear,
        decimal KwhPerMonth,
        decimal CostPerYearTjs,
        decimal CostPerMonthTjs,
        decimal? BaselineKwhPerYear,
        decimal? BaselineCostPerYearTjs,
        decimal? SavingKwhPerYear,
        decimal? SavingCostPerYearTjs);

    public static Estimate Calculate(decimal area, decimal kwhPerM2Year, decimal tariffTjs, decimal? baselineKwhPerM2Year)
    {
        if (area <= 0 || kwhPerM2Year < 0 || tariffTjs < 0)
            throw new ArgumentOutOfRangeException(nameof(area), "Площадь, потребление и тариф должны быть неотрицательными");

        var kwhYear = Round(area * kwhPerM2Year, 1);
        var costYear = Round(area * kwhPerM2Year * tariffTjs, 2);
        decimal? baseKwh = null, baseCost = null, saveKwh = null, saveCost = null;
        if (baselineKwhPerM2Year is { } b and >= 0)
        {
            baseKwh = Round(area * b, 1);
            baseCost = Round(area * b * tariffTjs, 2);
            saveKwh = baseKwh - kwhYear;
            saveCost = baseCost - costYear;
        }
        return new Estimate(kwhYear, Round(kwhYear / 12, 1), costYear, Round(costYear / 12, 2), baseKwh, baseCost, saveKwh, saveCost);
    }

    private static decimal Round(decimal v, int digits) => Math.Round(v, digits, MidpointRounding.AwayFromZero);
}
