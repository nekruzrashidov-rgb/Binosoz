using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Ступень цены за м²: этажи FromFloor–ToFloor. С PricePerM2To цена меняется по этажам ровно от PricePerM2 (на FromFloor)
// до PricePerM2To (на ToFloor), каждый этаж — округление до 1 единицы валюты; без него — одна цена на все этажи ступени.
public class PriceStep
{
    public int FromFloor { get; set; }
    public int ToFloor { get; set; }
    public decimal PricePerM2 { get; set; }
    public decimal? PricePerM2To { get; set; }
}

// Правило цены блока (данные, а не код): ступени по этажам и своя цена м² пентхаусов на последнем этаже; валюта каталога
// блока. Редактируют Owner и ObjectAdmin (CRM → «Каталог» → блок → «Цены по этажам»), «Пересчитать цены» применяет его
// к свободным квартирам блока.
public static class PriceRules
{
    // Цена м² этажа: пентхаус на последнем этаже — PenthousePricePerM2; иначе ступень этажа; этаж ниже первой ступени — её
    // начальная цена, выше последней — её конечная. Ступеней нет — null (правило не задано).
    public static decimal? PricePerM2(IReadOnlyList<PriceStep>? steps, int floor)
    {
        if (steps is not { Count: > 0 })
            return null;
        var ordered = steps.OrderBy(s => s.FromFloor).ToList();
        var step = ordered.FirstOrDefault(s => floor >= s.FromFloor && floor <= s.ToFloor);
        if (step is null)
            return floor < ordered[0].FromFloor ? ordered[0].PricePerM2 : ordered[^1].PricePerM2To ?? ordered[^1].PricePerM2;
        if (step.PricePerM2To is not { } to || step.ToFloor == step.FromFloor)
            return step.PricePerM2;
        var k = (decimal)(floor - step.FromFloor) / (step.ToFloor - step.FromFloor);
        return Math.Round(step.PricePerM2 + (to - step.PricePerM2) * k, 0, MidpointRounding.AwayFromZero);
    }

    public static decimal? PricePerM2(Building b, int floor, bool penthouse) =>
        penthouse && b.PenthousePricePerM2 is { } p ? p : PricePerM2(b.PriceSteps, floor);

    // Цена помещения по правилу блока: машино-место — своя цена за место (Fixed), магазин — цена м² коммерции, пентхаус —
    // цена м² пентхаусов, квартира — ступень этажа. Не задано — null (цена не меняется).
    public static (decimal? PricePerM2, decimal? Fixed) PriceOf(Building b, Apartment a, int floor) => a.Kind switch
    {
        Enums.ApartmentKind.Parking => (null, b.ParkingSpotPrice),
        Enums.ApartmentKind.Commercial => (b.CommercialPricePerM2, null),
        Enums.ApartmentKind.Penthouse => (b.PenthousePricePerM2 ?? PricePerM2(b.PriceSteps, floor), null),
        _ => (PricePerM2(b, floor, floor == b.FloorsCount), null),
    };
}

public static class PriceDefaults
{
    // Правило сида ЖК «Rich Tower» (USD): 3–6 этаж — 1 200 $/м²; 7–19 — дешевле с каждым этажом, ровно от 1 200 (6-й)
    // до 900 $/м² (19-й), ≈ −23,08 $ за этаж, до 1 $ (поэтому ступени 3–5 и 6–19); 20 и выше, кроме последнего, — 900;
    // последний этаж — пентхаусы, 1 400 $/м².
    public const Currency Currency = Enums.Currency.USD;
    public const decimal PenthousePricePerM2 = 1400m;
    // Магазины 1–2 этажа — 1 500 $/м², машино-место — 20 000 $ (не от площади).
    public const decimal CommercialPricePerM2 = 1500m, ParkingSpotPrice = 20000m;

    public static List<PriceStep> Steps(int floors) =>
    [
        new() { FromFloor = 3, ToFloor = 5, PricePerM2 = 1200m },
        new() { FromFloor = 6, ToFloor = 19, PricePerM2 = 1200m, PricePerM2To = 900m },
        new() { FromFloor = 20, ToFloor = Math.Max(20, floors - 1), PricePerM2 = 900m },
    ];
}
