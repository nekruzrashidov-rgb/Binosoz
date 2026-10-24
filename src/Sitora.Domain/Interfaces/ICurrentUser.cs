using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsInRole(string role);

    // Сотрудники, которым видны все сделки: менеджер, бухгалтер, админ.
    bool IsDealStaff => IsInRole(RoleNames.Manager) || IsInRole(RoleNames.Accountant) || IsInRole(RoleNames.Admin);
}
