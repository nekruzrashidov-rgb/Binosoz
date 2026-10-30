using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

// Заявка «хочу стать застройщиком на платформе» с публичной страницы /register.
// Организации не принадлежит: её рассматривает создатель платформы и при одобрении создаёт организацию.
public class OrgApplication : BaseEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;

    // Номер в виде +992XXXXXXXXX.
    public string ContactPhone { get; set; } = string.Empty;
    public string OwnerFullName { get; set; } = string.Empty;
    public string? Comment { get; set; }

    public OrgApplicationStatus Status { get; set; } = OrgApplicationStatus.Pending;

    // Причина отказа.
    public string? ReviewNote { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Организация, созданная по одобренной заявке.
    public Guid? OrganizationId { get; set; }
}
