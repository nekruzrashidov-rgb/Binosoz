namespace Sitora.Domain.DTOs.Platform;

// Пользователь платформы в разделе суперадмина. Role — главная роль; LastLoginAt — по журналу входов;
// DealsCount — сделки покупателя (у сотрудника 0); Status — Active или Blocked.
public record PlatformUserResponse(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? Role,
    IReadOnlyList<string> Roles,
    Guid? OrganizationId,
    string? OrganizationName,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string Status,
    int DealsCount);
