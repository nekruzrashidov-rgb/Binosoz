namespace Sitora.Domain.Entities;

public class Building : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string NameRu { get; set; } = string.Empty;
    public string NameTg { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;

    // Число жилых этажей; коммерческий этаж 0 сюда не входит.
    public int FloorsCount { get; set; }
    public string? DescriptionRu { get; set; }
    public string? DescriptionTg { get; set; }
    public string? ModelUrl { get; set; }

    public List<Floor> Floors { get; set; } = [];
}
