using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Пользователи всей платформы: сотрудники организаций, покупатели и суперадмины.
// Последний вход — по журналу (AppUser / Login), отдельного поля нет.
public class PlatformUserService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    ICurrentUser currentUser,
    StaffAccounts accounts,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<PlatformUserService> logger) : IPlatformUserService
{
    private const string UserNotFound = "Пользователь не найден";

    // Блокировка — выключенная учётная запись и бессрочная (на 100 лет) блокировка входа Identity.
    private static readonly TimeSpan BlockFor = TimeSpan.FromDays(365 * 100);

    public async Task<PagedResult<PlatformUserResponse>> GetAsync(string? role, Guid? organizationId, string? search, int page, int pageSize,
        CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var users = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(role))
        {
            var roleName = RoleNames.All.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw new BusinessRuleException("Неизвестная роль: " + role);
            users = users.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == roleName)));
        }
        if (organizationId is { } org)
            users = users.Where(u => u.OrganizationId == org);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = "%" + search.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            users = users.Where(u => EF.Functions.ILike(u.FullName, like) || EF.Functions.ILike(u.Email ?? "", like)
                                     || EF.Functions.ILike(u.PhoneNumber ?? "", like));
        }

        var total = await users.CountAsync(ct);
        var ids = await users.OrderByDescending(u => u.CreatedAt).ThenBy(u => u.Id).Skip(p.Skip).Take(p.PageSize).Select(u => u.Id).ToListAsync(ct);
        var items = await ProjectAsync(ids, ct);
        return new PagedResult<PlatformUserResponse>(ids.Select(id => items[id]).ToList(), p.Page, p.PageSize, total);
    }

    public async Task<PlatformUserResponse> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        (await ProjectAsync([id], ct)).GetValueOrDefault(id) ?? throw new NotFoundException(UserNotFound);

    public async Task<PlatformUserResponse> BlockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id);
        if (currentUser.UserId == id)
            throw new BusinessRuleException("Нельзя заблокировать самого себя");
        if (await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin))
            throw new BusinessRuleException("Суперадмина заблокировать нельзя");

        var before = new { user.IsActive, user.LockoutEnd };
        user.IsActive = false;
        await userManager.SetLockoutEnabledAsync(user, true);
        await userManager.SetLockoutEndDateAsync(user, timeProvider.GetUtcNow().Add(BlockFor));
        audit.Record(nameof(AppUser), user.Id, "Blocked", before, new { user.IsActive, user.LockoutEnd, BySuperAdmin = currentUser.UserId }, user.OrganizationId);
        await db.SaveChangesAsync(ct);
        await RevokeSessionsAsync(id, ct);
        logger.LogWarning("Суперадмин {AdminId} заблокировал пользователя {UserId}", currentUser.UserId, id);
        return await GetByIdAsync(id, ct);
    }

    public async Task<PlatformUserResponse> UnblockAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id);
        var before = new { user.IsActive, user.LockoutEnd };
        user.IsActive = true;
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        audit.Record(nameof(AppUser), user.Id, "Unblocked", before, new { user.IsActive, BySuperAdmin = currentUser.UserId }, user.OrganizationId);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    // Новый пароль — только сотрудникам (покупатели входят по коду из почты): прежний перестаёт работать, сессии закрываются.
    public async Task ResetPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var user = await FindAsync(id);
        if ((await userManager.GetRolesAsync(user)).All(r => r == RoleNames.Customer))
            throw new BusinessRuleException("Покупатели входят по коду из почты — пароль им не нужен");
        if (string.IsNullOrEmpty(user.Email))
            throw new BusinessRuleException("У пользователя нет почты: новый пароль некуда отправить");

        var password = StaffAccounts.GeneratePassword();
        if (await userManager.HasPasswordAsync(user))
            await userManager.RemovePasswordAsync(user);
        var result = await userManager.AddPasswordAsync(user, password);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.ResetAccessFailedCountAsync(user);
        audit.Record(nameof(AppUser), user.Id, "PasswordResetBySuperAdmin", null, new { BySuperAdmin = currentUser.UserId }, user.OrganizationId);
        await db.SaveChangesAsync(ct);
        await RevokeSessionsAsync(id, ct);
        await accounts.SendNewPasswordAsync(user, password, ct);
    }

    private async Task<Dictionary<Guid, PlatformUserResponse>> ProjectAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
            .Select(u => new
            {
                u.Id, u.FullName, u.Email, u.PhoneNumber, u.OrganizationId, u.CreatedAt, u.IsActive,
                OrganizationName = db.Organizations.Where(o => o.Id == u.OrganizationId).Select(o => o.Name).FirstOrDefault(),
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!).ToList(),
                LastLogin = db.AuditLogs.Where(a => a.Entity == nameof(AppUser) && a.EntityId == u.Id && a.Action == "Login").Max(a => (DateTime?)a.At),
                Deals = db.Deals.Count(d => d.Customer.UserId == u.Id)
            })
            .ToListAsync(ct);
        return rows.ToDictionary(u => u.Id, u => new PlatformUserResponse(u.Id, u.FullName, u.Email, u.PhoneNumber, AuthService.PrimaryRole(u.Roles),
            u.Roles, u.OrganizationId, u.OrganizationName, u.CreatedAt, u.LastLogin, u.IsActive ? "Active" : "Blocked", u.Deals));
    }

    private async Task<AppUser> FindAsync(Guid id) =>
        await userManager.FindByIdAsync(id.ToString()) ?? throw new NotFoundException(UserNotFound);

    private Task RevokeSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
