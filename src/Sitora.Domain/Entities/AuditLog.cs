namespace Sitora.Domain.Entities;

public class AuditLog : BaseEntity
{
    // null — изменение сделала система (сидер, фоновая задача).
    public Guid? UserId { get; set; }
    public string Entity { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
    public DateTime At { get; set; }
}
