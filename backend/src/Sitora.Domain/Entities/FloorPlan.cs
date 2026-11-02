namespace Sitora.Domain.Entities;

// Интерактивный план этажа: SVG, где каждая квартира — <rect id="apt-{apartmentId}">.
// Цвета по статусу и подсказки подставляются при выдаче, в шаблоне хранится только разметка.
public class FloorPlan : BaseEntity
{
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int Floor { get; set; }
    public string SvgTemplate { get; set; } = string.Empty;
}
