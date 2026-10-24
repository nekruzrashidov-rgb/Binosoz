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
    IAuditService audit,
    TimeProvider timeProvider) : IUserService
{
    private static readonly string[] StaffRoles = [RoleNames.Admin, RoleNames.Manager, RoleNames.Accountant, RoleNames.Content];

    public async Task<PagedResult<UserResponse>> GetAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var total = await db.Users.CountAsync(ct);
        var rows = await db.Users.AsNoTracking()
            .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id)
            .Skip(p.Skip).Take(p.PageSize)
            .Select(u => new
            {
                u.Id, u.Email, u.PhoneNumber, u.FullName, u.IsActive, u.CreatedAt,
                Roles = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name!)
                    .ToList()
            })
            .ToListAsync(ct);

        var items = rows.Select(u => new UserResponse(u.Id, u.Email, u.PhoneNumber, u.FullName, u.Roles, u.IsActive, u.CreatedAt)).ToList();
        return new PagedResult<UserResponse>(items, p.Page, p.PageSize, total);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (!StaffRoles.Contains(request.Role))
            throw new BusinessRuleException("Роль должна быть одной из: " + string.Join(", ", StaffRoles) +
                                            ". Покупатели заводятся сами при входе по SMS");

        var email = request.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
            phone = PhoneNumber.Normalize(request.Phone) ?? throw new BusinessRuleException("Неверный номер телефона");

        var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = request.FullName.Trim(), PhoneNumber = phone };
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(user, request.Role);
        audit.Record(nameof(AppUser), user.Id, "Created", null, new { user.Email, user.FullName, request.Role });
        await db.SaveChangesAsync(ct);

        return new UserResponse(user.Id, user.Email, user.PhoneNumber, user.FullName, [request.Role], user.IsActive, user.CreatedAt);
    }

    public async Task<UserResponse> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException("Пользователь не найден");
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
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
        }

        var roles = await userManager.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email, user.PhoneNumber, user.FullName, roles.ToList(), user.IsActive, user.CreatedAt);
    }
}
