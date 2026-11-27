namespace Sitora.Domain.DTOs.Common;

public record AuditQuery(string? Entity, Guid? EntityId, DateTime? From, DateTime? To, int Page = 1, int PageSize = PageRequest.DefaultPageSize);

// UserId / UserEmail / UserName — автор записи (C-06): пользователь из записи или, если его нет и объект — сам
// пользователь (вход до аутентификации), он сам; BySystem — сидер и фоновые задачи. EntityName и ApartmentNumber —
// понятное имя объекта (ФИО, имя лида, номер квартиры) вместо идентификатора.
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
    Guid? ImpersonatedById = null,
    string? UserName = null,
    bool BySystem = false,
    string? EntityName = null,
    int? ApartmentNumber = null,
    // «A-45» у помещений блока ЖК (UnitNames.Short); у дома без блока — null.
    string? ApartmentCode = null);
