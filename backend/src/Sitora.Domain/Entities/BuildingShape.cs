namespace Sitora.Domain.Entities;

// Место блока на общем участке ЖК (метры): сдвиг начала координат его контура и поворот по часовой стрелке.
public class SitePlacement
{
    public double X { get; set; }
    public double Y { get; set; }
    public double RotationDeg { get; set; }
}

// Уступ: с этажа FromFloor и выше — контур меньше основного (террасы на крыше уступа).
public class BuildingSetback
{
    public int FromFloor { get; set; }
    public List<double[]> Footprint { get; set; } = [];
}

// План общего участка ЖК (метры, те же оси, что у SitePosition блоков): контур участка и зоны двора. Зона рисуется на сайте
// только если её удобство есть в Amenities хотя бы одного блока (честные данные): площадка — playground, поле —
// football_field, рампа паркинга — underground_parking; дорожки и газон — без условия.
public class SitePlan
{
    public List<double[]> Boundary { get; set; } = [];
    public List<SiteZone> Zones { get; set; } = [];
}

public class SiteZone
{
    // playground | football_field | parking_ramp | path | lawn
    public string Kind { get; set; } = string.Empty;
    public List<double[]> Polygon { get; set; } = [];
}
