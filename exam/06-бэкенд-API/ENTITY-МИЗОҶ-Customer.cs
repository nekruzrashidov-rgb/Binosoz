namespace Sitora.Domain.Entities;

public class Customer : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid? HomeBuildingId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }

    public string? PassportDataEncrypted { get; set; }

    public Guid? UserId { get; set; }
    public AppUser? User { get; set; }

    public List<Deal> Deals { get; set; } = [];
}
