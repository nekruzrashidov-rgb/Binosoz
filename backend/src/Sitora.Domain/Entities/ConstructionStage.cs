namespace Sitora.Domain.Entities;

public class ConstructionStage : BaseEntity
{
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public int Percent { get; set; }
    public int SortOrder { get; set; }

    public List<ConstructionPhoto> Photos { get; set; } = [];
}

public class ConstructionPhoto : BaseEntity
{
    public Guid StageId { get; set; }
    public ConstructionStage Stage { get; set; } = null!;

    public string FileKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public DateTime TakenAt { get; set; }
}
