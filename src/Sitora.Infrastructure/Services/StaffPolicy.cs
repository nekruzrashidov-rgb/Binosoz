using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Кто кого может назначать и на какие дома. Общие правила для приглашений и создания сотрудников.
public class StaffPolicy(AppDbContext db, ICurrentUser currentUser)
{
    private static readonly string[] OwnerAssignable = [RoleNames.ObjectAdmin, RoleNames.Manager, RoleNames.Accountant, RoleNames.Content];
    private static readonly string[] ObjectAdminAssignable = [RoleNames.Manager, RoleNames.Accountant, RoleNames.Content];

    public Guid RequireOrganization() =>
        currentUser.OrganizationId
        ?? throw new BusinessRuleException("Выберите организацию: войдите как её владелец");

    public bool IsBuildingAdmin => currentUser.IsInRole(RoleNames.ObjectAdmin);

    // Проверяет роль и дома нового сотрудника; возвращает итоговый список домов (пустой — все дома организации).
    public async Task<List<Guid>> ValidateAsync(string role, IReadOnlyList<Guid>? buildingIds, CancellationToken ct)
    {
        RequireOrganization();
        var allowed = IsBuildingAdmin ? ObjectAdminAssignable : OwnerAssignable;
        if (!allowed.Contains(role))
            throw new BusinessRuleException("Роль должна быть одной из: " + string.Join(", ", allowed));

        var requested = (buildingIds ?? []).Distinct().ToList();
        if (IsBuildingAdmin)
        {
            // Админ объекта зовёт людей только в свой дом.
            var own = currentUser.BuildingIds ?? [];
            if (requested.Count > 0 && requested.Except(own).Any())
                throw new ForbiddenException("Админ объекта назначает сотрудников только в свой дом");
            return own.ToList();
        }

        await EnsureBuildingsAsync(role, requested, ct);
        return requested;
    }

    // Дома должны быть домами этой организации (и видны текущему руководителю); у админа объекта — ровно один.
    public async Task EnsureBuildingsAsync(string role, List<Guid> buildingIds, CancellationToken ct)
    {
        if (role == RoleNames.ObjectAdmin && buildingIds.Count != 1)
            throw new BusinessRuleException("Админу объекта назначается ровно один дом");

        if (buildingIds.Count > 0)
        {
            var found = await db.Buildings.CountAsync(b => buildingIds.Contains(b.Id), ct);
            if (found != buildingIds.Count)
                throw new NotFoundException("Дом не найден");
        }
    }
}
