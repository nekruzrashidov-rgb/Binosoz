namespace Sitora.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? OrganizationId { get; set; }

    public Guid? ImpersonatedById { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime At { get; set; }

    // Адрес, с которого пришёл запрос (за Caddy — из X-Forwarded-For); null — фоновая задача или сидер.
    public string? Ip { get; set; }
}
