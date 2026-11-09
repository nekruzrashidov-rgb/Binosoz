namespace Sitora.Domain.DTOs.Platform;

// Журнал всей платформы: фильтры по организации, действию и датам [From; To).
public record PlatformActivityQuery(Guid? OrganizationId, string? Action, DateTime? From, DateTime? To, int Page = 1, int PageSize = 50);

public record PlatformActivityItem(
    Guid Id,
    DateTime At,
    string Action,
    string Entity,
    Guid EntityId,
    Guid? OrganizationId,
    string? OrganizationName,
    string? UserEmail,
    string? Ip,
    bool Impersonated);

// Уведомление от платформы: всем пользователям организации или всем пользователям всех организаций (OrganizationId = null).
// Type: info, warning, error.
public record PlatformNotificationRequest(Guid? OrganizationId, string Type, string Title, string Message);

public record PlatformNotificationResponse(int Recipients);

public record PlatformSettingsResponse(int? MaxOrganizations, bool DemoMode, string Version, string? ContactEmail, int Organizations);

// PATCH: меняются только переданные поля; ClearMaxOrganizations = true снимает ограничение.
public record UpdatePlatformSettingsRequest(int? MaxOrganizations, bool? ClearMaxOrganizations, bool? DemoMode, string? ContactEmail);
