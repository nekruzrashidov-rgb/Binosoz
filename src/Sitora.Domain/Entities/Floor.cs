namespace Sitora.Domain.Entities;

public class Floor : BaseEntity
{
    public const int CommercialFloorNumber = 0;

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public int Number { get; set; }
    public string? PlanImageUrl { get; set; }

    public List<Apartment> Apartments { get; set; } = [];
}
