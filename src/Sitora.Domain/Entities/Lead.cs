using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

public class Lead : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Comment { get; set; }

    public Guid? ApartmentId { get; set; }
    public Apartment? Apartment { get; set; }

    public LeadSource Source { get; set; } = LeadSource.Website;
    public LeadStatus Status { get; set; } = LeadStatus.New;

    public Guid? ManagerId { get; set; }
    public AppUser? Manager { get; set; }
}
