namespace Sitora.Domain.DTOs.Common;

public record AuditQuery(string? Entity, Guid? EntityId, DateTime? From, DateTime? To, int Page = 1, int PageSize = PageRequest.DefaultPageSize);

public record AuditLogResponse(
    Guid Id,
    Guid? UserId,
    string? UserEmail,
    string Entity,
    Guid EntityId,
    string Action,
    string? Before,
    string? After,
    DateTime At,
    Guid? OrganizationId = null,
    Guid? ImpersonatedById = null);
