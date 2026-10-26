using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class UserService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    StaffPolicy policy,
    IAuditService audit,
    TimeProvider timeProvider) : IUserService
{
    private const string UserNotFound = "Пользователь не найден";

    // Сотрудники, которых видит текущий руководитель. Таблица пользователей не под фильтром организации
    // (она нужна Identity при входе), поэтому ограничение задаётся здесь явно.
    private IQueryable<AppUser> VisibleStaff()
    {
        var users = db.Users.Where(u => u.OrganizationId != null);
        if (currentUser.OrganizationId is { } organizationId)
            users = users.Where(u => u.OrganizationId == organizationId);
        if (currentUser.BuildingIds is { } buildings)
            users = users.Where(u => db.UserBuildings.Any(ub => ub.UserId == u.Id && buildings.Contains(ub.BuildingId)));
        return users;
    }

    public async Task<PagedResult<UserResponse>> GetAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var query = VisibleStaff();
        var total = await query.CountAsync(ct);
        var rows = await query.AsNoTracking()
            .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.PhoneNumber, u.FullName, u.IsActive, u.CreatedAt, u.OrganizationId,
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToList(),
                Buildings = db.UserBuildings.Where(ub => ub.UserId == u.Id).Select(ub => ub.BuildingId).ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(u => new UserResponse(u.Id, u.Email, u.PhoneNumber, u.FullName, u.Roles, u.IsActive, u.CreatedAt,
            u.OrganizationId, u.Buildings)).ToList();
        return new PagedResult<UserResponse>(items, p.Page, p.PageSize, total);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var buildings = await policy.ValidateAsync(request.Role, request.BuildingIds, ct);
        var organizationId = policy.RequireOrganization();

        var email = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
            phone = PhoneNumber.Normalize(request.Phone) ?? throw new BusinessRuleException("Неверный номер телефона");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = request.FullName.Trim(),
            PhoneNumber = phone,
            OrganizationId = organizationId
        };
        // Пароль необязателен: сотрудник может входить по коду из почты.
        var result = string.IsNullOrEmpty(request.Password)
            ? await userManager.CreateAsync(user)
            : await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, request.Role);
        foreach (var buildingId in buildings)
            db.UserBuildings.Add(new UserBuilding { UserId = user.Id, BuildingId = buildingId });
        audit.Record(nameof(AppUser), user.Id, "Created", null, new { user.Email, user.FullName, request.Role, BuildingIds = buildings });
        await db.SaveChangesAsync(ct);

        return new UserResponse(user.Id, user.Email, user.PhoneNumber, user.FullName, [request.Role], user.IsActive, user.CreatedAt,
            organizationId, buildings);
    }

    public async Task<UserResponse> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await FindManageableAsync(id, ct);
        if (!request.IsActive && currentUser.UserId == id)
            throw new BusinessRuleException("Нельзя заблокировать самого себя");

        var before = new { user.IsActive };
        user.IsActive = request.IsActive;
        audit.Record(nameof(AppUser), user.Id, request.IsActive ? "Unblocked" : "Blocked", before, new { user.IsActive });
        await db.SaveChangesAsync(ct);

        // Разблокировка снимает и временную блокировку входа после неудачных попыток.
        if (request.IsActive)
        {
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
        }

        // Заблокированный пользователь сразу теряет все refresh-токены.
        if (!request.IsActive)
            await RevokeSessionsAsync(id, ct);

        return await ToResponseAsync(user, ct);
    }

    // Дома сотрудника меняет владелец. Сессии сотрудника отзываются: новый список домов попадёт в токен при следующем входе.
    public async Task<UserResponse> SetBuildingsAsync(Guid id, SetUserBuildingsRequest request, CancellationToken ct = default)
    {
        var user = await FindManageableAsync(id, ct);
        var roles = await userManager.GetRolesAsync(user);
        if (roles.Contains(RoleNames.Owner))
            throw new BusinessRuleException("Владельцу доступны все дома организации");

        var buildings = request.BuildingIds.Distinct().ToList();
        await policy.EnsureBuildingsAsync(roles.Contains(RoleNames.ObjectAdmin) ? RoleNames.ObjectAdmin : RoleNames.Manager, buildings, ct);

        var current = await db.UserBuildings.Where(ub => ub.UserId == id).ToListAsync(ct);
        var before = current.Select(ub => ub.BuildingId).ToList();
        db.UserBuildings.RemoveRange(current);
        foreach (var buildingId in buildings)
            db.UserBuildings.Add(new UserBuilding { UserId = id, BuildingId = buildingId });
        audit.Record(nameof(AppUser), id, "BuildingsChanged", new { BuildingIds = before }, new { BuildingIds = buildings });
        await db.SaveChangesAsync(ct);
        await RevokeSessionsAsync(id, ct);

        return await ToResponseAsync(user, ct);
    }

    // Сотрудник из своей организации (и своего дома для админа объекта); руководителей админ объекта не трогает.
    private async Task<AppUser> FindManageableAsync(Guid id, CancellationToken ct)
    {
        var user = await VisibleStaff().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(UserNotFound);
        if (currentUser.IsInRole(RoleNames.ObjectAdmin) && currentUser.UserId != id)
        {
            var roles = await userManager.GetRolesAsync(user);
            if (roles.Contains(RoleNames.Owner) || roles.Contains(RoleNames.ObjectAdmin))
                throw new ForbiddenException("Админ объекта управляет только сотрудниками своего дома");
        }

        return user;
    }

    private async Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task<UserResponse> ToResponseAsync(AppUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var buildings = await db.UserBuildings.Where(ub => ub.UserId == user.Id).Select(ub => ub.BuildingId).ToListAsync(ct);
        return new UserResponse(user.Id, user.Email, user.PhoneNumber, user.FullName, roles.ToList(), user.IsActive, user.CreatedAt,
            user.OrganizationId, buildings);
    }
}
