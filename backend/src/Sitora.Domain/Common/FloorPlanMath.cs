namespace Sitora.Domain.Common;

// Раскладка этажа блока (как на сайте, S.geom.floor): квартиры — полоса вдоль фасада глубиной Depth, по порядку PlanSlot
// от первой вершины контура по часовой стрелке; внутри полосы — коридор и лифтовой узел. Площадь полосы = площадь контура
// минус внутренний контур (PlanGeometry.OffsetInward); площади квартир — доли полосы по весам, до 0,1 м². Сайт делит полосу
// по этим площадям и получает те же многоугольники квартир.
public static class FloorPlanMath
{
    // Глубина полосы квартир типового этажа и этажа пентхаусов, м (на сайте — те же числа).
    public const double Depth = 7, PenthouseDepth = 6.5;

    public static double BandArea(IReadOnlyList<double[]> footprint, double depth)
    {
        var ring = PlanGeometry.Vertices(footprint);
        return PlanGeometry.Area(ring) - InnerArea(footprint, depth);
    }

    public static double InnerArea(IReadOnlyList<double[]> footprint, double depth)
    {
        var inner = PlanGeometry.OffsetInward(PlanGeometry.Vertices(footprint), depth);
        return inner.Count < 3 ? 0 : PlanGeometry.Area(inner);
    }

    public static decimal[] UnitAreas(IReadOnlyList<double[]> footprint, double depth, IReadOnlyList<double> weights)
    {
        var band = BandArea(footprint, depth);
        var sum = weights.Sum();
        return weights.Select(w => Math.Round((decimal)(band * w / sum), 1, MidpointRounding.AwayFromZero)).ToArray();
    }
}
