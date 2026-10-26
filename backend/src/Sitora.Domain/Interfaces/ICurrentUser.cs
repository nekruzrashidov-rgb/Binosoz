using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsInRole(string role);

    // Организация сотрудника (или организация, в которую вошёл создатель платформы). null — нет ограничения.
    Guid? OrganizationId { get; }

    // Доступные дома; null — все дома организации.
    IReadOnlyList<Guid>? BuildingIds { get; }

    // Создатель платформы, который сейчас действует «как владелец».
    Guid? ImpersonatorId { get; }

    // Данные ограничены организацией: сотрудник или режим «войти как владелец».
    // Гость, покупатель (свои сделки проверяет DealAccess) и создатель платформы видят всё.
    bool IsRestricted => OrganizationId is not null;

    bool IsLeader => IsInRole(RoleNames.PlatformOwner) || IsInRole(RoleNames.Owner) || IsInRole(RoleNames.ObjectAdmin);

    // Сотрудники, которым видны все сделки своих домов: менеджер, бухгалтер и руководители.
    bool IsDealStaff => IsLeader || IsInRole(RoleNames.Manager) || IsInRole(RoleNames.Accountant);

    bool CanAccessBuilding(Guid buildingId) => BuildingIds is null || BuildingIds.Contains(buildingId);
}
