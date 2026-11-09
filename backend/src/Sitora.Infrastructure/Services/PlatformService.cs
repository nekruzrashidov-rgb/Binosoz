using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

// Действия суперадмина: организации и вход «как владелец».
public class PlatformService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    AuthService auth,
    JwtTokenService tokens,
    ICurrentUser currentUser,
    IAuditService audit,
    StaffAccounts accounts,
    ITariffService tariffs,
    IPlatformToolsService platformTools,
    ILogger<PlatformService> logger) : IPlatformService
{
    // Организации с логотипом, владельцами, домами, счётчиками сделок и покупателей, последней активностью и тарифом.
    public async Task<IReadOnlyList<OrganizationResponse>> GetOrganizationsAsync(CancellationToken ct = default) =>
        await OrganizationsAsync(null, ct);

    private async Task<List<OrganizationResponse>> OrganizationsAsync(Guid? onlyId, CancellationToken ct)
    {
        var rows = await db.Organizations.AsNoTracking()
            .Where(o => onlyId == null || o.Id == onlyId)
            .OrderBy(o => o.Name)
            .Select(o => new
            {
                o.Id, o.Name, o.Slug, o.IsActive, o.CreatedAt, o.CustomDomain, o.LogoUrl,
                Buildings = o.Buildings.OrderBy(b => b.NameRu).Select(b => b.NameRu).ToList(),
                Users = db.Users.Count(u => u.OrganizationId == o.Id),
                Deals = db.Deals.Count(d => d.Apartment.Floor.Building.OrganizationId == o.Id),
                Customers = db.Customers.Count(c => c.OrganizationId == o.Id),
                LastActivity = db.AuditLogs.Where(a => a.OrganizationId == o.Id).Max(a => (DateTime?)a.At)
            })
            .ToListAsync(ct);
        var ids = rows.Select(o => o.Id).ToList();
        var ownerRoleId = await db.Roles.Where(r => r.Name == RoleNames.Owner).Select(r => r.Id).SingleAsync(ct);
        var owners = (await db.Users.AsNoTracking()
                .Where(u => u.OrganizationId != null && ids.Contains(u.OrganizationId.Value)
                            && db.UserRoles.Any(ur => ur.UserId == u.Id && ur.RoleId == ownerRoleId))
                .Select(u => new { u.Id, u.OrganizationId, u.FullName, u.Email, u.PhoneNumber })
                .ToListAsync(ct))
            .ToLookup(u => u.OrganizationId!.Value);
        var tariffsNow = await tariffs.CurrentAsync(ids, ct);
        return rows.Select(o =>
        {
            var orgOwners = owners[o.Id].OrderBy(u => u.Email).ToList();
            return new OrganizationResponse(o.Id, o.Name, o.Slug, o.IsActive, o.Buildings.Count, o.Users, o.CreatedAt,
                o.CustomDomain, o.LogoUrl, orgOwners.Select(u => u.Email ?? string.Empty).ToList(), o.Buildings,
                o.Deals, o.Customers, o.LastActivity, tariffsNow.GetValueOrDefault(o.Id),
                orgOwners.Select(u => new OrganizationOwnerInfo(u.Id, u.FullName, u.Email, u.PhoneNumber)).ToList());
        }).ToList();
    }

    // Карточка организации: данные списка и статистика по домам, квартирам и платежам, история тарифов.
    public async Task<OrganizationDetailsResponse> GetOrganizationAsync(Guid organizationId, CancellationToken ct = default)
    {
        var organization = (await OrganizationsAsync(organizationId, ct)).SingleOrDefault()
            ?? throw new NotFoundException("Организация не найдена");
        var buildings = await db.Buildings.AsNoTracking().Where(b => b.OrganizationId == organizationId)
            .OrderBy(b => b.NameRu)
            .Select(b => new OrganizationBuildingBrief(b.Id, b.NameRu,
                db.Apartments.Count(a => a.Floor.BuildingId == b.Id),
                db.Apartments.Count(a => a.Floor.BuildingId == b.Id && a.Status == ApartmentStatus.Free)))
            .ToListAsync(ct);
        var sold = await db.Apartments.CountAsync(a => a.Floor.Building.OrganizationId == organizationId && a.Status == ApartmentStatus.Sold, ct);
        var paid = await db.Payments.Where(p => p.Deal.Apartment.Floor.Building.OrganizationId == organizationId).SumAsync(p => (decimal?)p.AmountTjs, ct) ?? 0;
        var history = await db.OrganizationTariffs.AsNoTracking().Where(t => t.OrganizationId == organizationId)
            .OrderByDescending(t => t.StartsAt)
            .Select(t => new OrganizationTariffResponse(t.Id, t.OrganizationId, t.TariffPlanId, t.TariffPlan.Name, t.TariffPlan.Price,
                t.TariffPlan.Currency, t.StartsAt, t.EndsAt, t.IsPaid))
            .ToListAsync(ct);
        return new OrganizationDetailsResponse(organization, paid, buildings.Sum(b => b.ApartmentsCount), buildings.Sum(b => b.FreeApartments),
            sold, buildings, history);
    }

    // Название и короткое имя; короткое имя — по тем же правилам, что при создании, и не занято другой организацией.
    public async Task<OrganizationDetailsResponse> UpdateOrganizationAsync(Guid organizationId, UpdateOrganizationRequest request, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
            throw new BusinessRuleException("Название организации — от 1 до 200 символов");
        var slug = organization.Slug;
        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            slug = BrandRules.NormalizeSlug(request.Slug) ?? throw new BusinessRuleException(BrandRules.SlugMessage);
            if (slug != organization.Slug && BrandRules.IsReserved(slug))
                throw new BusinessRuleException($"Короткое имя «{slug}» зарезервировано платформой, выберите другое");
            if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == slug && o.Id != organizationId, ct))
                throw new ConflictException("Организация с таким коротким именем уже есть");
        }

        var before = new { organization.Name, organization.Slug };
        organization.Name = name;
        organization.Slug = slug;
        audit.Record(nameof(Organization), organization.Id, "Updated", before, new { organization.Name, organization.Slug }, organization.Id);
        await db.SaveChangesAsync(ct);
        return await GetOrganizationAsync(organizationId, ct);
    }

    // Новый владелец организации по имени и фамилии; пароль генерируется и приходит письмом.
    public Task<UserResponse> InviteOwnerAsync(Guid organizationId, InviteOwnerRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            throw new BusinessRuleException("Укажите имя и фамилию владельца");
        return AddOwnerAsync(organizationId, new AddOwnerRequest(request.Email, $"{request.FirstName.Trim()} {request.LastName.Trim()}", null), ct);
    }

    // Приостановка закрывает вход сотрудникам организации и её сайт; выданные сессии отзываются сразу.
    public async Task SetOrganizationActiveAsync(Guid organizationId, bool isActive, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        if (organization.IsActive == isActive)
            return;

        organization.IsActive = isActive;
        audit.Record(nameof(Organization), organization.Id, isActive ? "Activated" : "Suspended",
            new { IsActive = !isActive }, new { IsActive = isActive }, organization.Id);
        await db.SaveChangesAsync(ct);

        if (!isActive)
            await db.RefreshTokens
                .Where(t => t.RevokedAt == null && db.Users.Any(u => u.Id == t.UserId && u.OrganizationId == organizationId))
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        logger.LogInformation("Организация {OrganizationId} {State}", organizationId, isActive ? "снова работает" : "приостановлена");
    }

    // Новая организация сразу получает владельца; логин и пароль приходят ему письмом.
    public async Task<OrganizationResponse> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        var slug = BrandRules.NormalizeSlug(request.Slug) ?? throw new BusinessRuleException(BrandRules.SlugMessage);
        if (BrandRules.IsReserved(slug))
            throw new BusinessRuleException($"Короткое имя «{slug}» зарезервировано платформой, выберите другое");
        if (await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Slug == slug, ct))
            throw new ConflictException("Организация с таким коротким именем уже есть");

        var ownerEmail = EmailAddress.Normalize(request.OwnerEmail) ?? throw new BusinessRuleException("Неверный адрес почты владельца");
        if (await userManager.FindByEmailAsync(ownerEmail) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");
        // Лимит из «Настроек платформы» (если задан): и для создания вручную, и для одобрения заявки застройщика.
        await platformTools.EnsureOrganizationLimitAsync(ct);

        var organization = new Organization { Name = request.Name.Trim(), Slug = slug };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var (owner, password, _) = await accounts.CreateAsync(ownerEmail, request.OwnerFullName.Trim(), RoleNames.Owner, organization.Id, null, null);

        audit.Record(nameof(Organization), organization.Id, "Created", null,
            new { organization.Name, organization.Slug, OwnerEmail = ownerEmail }, organization.Id);
        await db.SaveChangesAsync(ct);

        // Логин и пароль — письмом; сбой почты не отменяет создание (владелец задаст пароль через «Забыли пароль?»).
        await accounts.SendCredentialsAsync(owner, password, organization.Name, ct);

        logger.LogInformation("Создана организация {OrganizationId} ({Slug})", organization.Id, slug);
        return new OrganizationResponse(organization.Id, organization.Name, organization.Slug, organization.IsActive, 0, 1,
            organization.CreatedAt);
    }

    // Ещё один владелец организации. Без пароля он генерируется и уходит владельцу письмом.
    public async Task<UserResponse> AddOwnerAsync(Guid organizationId, AddOwnerRequest request, CancellationToken ct = default)
    {
        await RequireSuperAdminAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        var email = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты владельца");
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName))
            throw new BusinessRuleException("Укажите имя владельца");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");
        await tariffs.EnsureCanAddUserAsync(organization.Id, ct);

        var (owner, password, generated) = await accounts.CreateAsync(email, fullName, RoleNames.Owner, organization.Id, null, request.Password);

        audit.Record(nameof(AppUser), owner.Id, "OwnerAdded", null, new { owner.Email, owner.FullName, OrganizationId = organization.Id }, organization.Id);
        await db.SaveChangesAsync(ct);
        if (generated)
            await accounts.SendCredentialsAsync(owner, password, organization.Name, ct);
        logger.LogInformation("В организацию {OrganizationId} добавлен владелец {UserId}", organization.Id, owner.Id);
        return new UserResponse(owner.Id, owner.Email, owner.PhoneNumber, owner.FullName, [RoleNames.Owner], owner.IsActive, owner.CreatedAt,
            organization.Id, []);
    }

    // id — организация или пользователь. Пометка imp видна в аудите и в каждом ответе (X-Impersonated-By).
    public async Task<AccessTokenResponse> ImpersonateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await RequireSuperAdminAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);
        return organization is not null
            ? await ImpersonateOrganizationAsync(user, organization, ct)
            : await ImpersonateUserAsync(user, id, ct);
    }

    // Под конкретным пользователем: его роли и дома, sub — он сам (иначе покупатель не увидел бы своих сделок),
    // а imp — создатель платформы; аудит пишет обоих.
    private async Task<AccessTokenResponse> ImpersonateUserAsync(AppUser platformOwner, Guid userId, CancellationToken ct)
    {
        var target = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Организация или пользователь не найдены");
        var roles = await userManager.GetRolesAsync(target);
        if (roles.Contains(RoleNames.SuperAdmin))
            throw new BusinessRuleException("Нельзя войти под создателем платформы");
        if (!target.IsActive)
            throw new BusinessRuleException("Пользователь заблокирован");

        var scope = await auth.ScopeOfAsync(target, ct) with { ImpersonatedBy = platformOwner.Id };
        var (token, expiresAt) = tokens.CreateAccessToken(target, roles, scope);
        audit.Record(nameof(AppUser), target.Id, "ImpersonationStarted", null,
            new { ImpersonatedBy = platformOwner.Id, platformOwner.Email, Roles = roles }, target.OrganizationId);
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Создатель платформы {UserId} вошёл под пользователем {TargetId}", platformOwner.Id, target.Id);
        return new AccessTokenResponse(token, expiresAt, await auth.BuildUserResponseAsync(target, roles, scope, ct));
    }

    // Токен без refresh: создатель платформы действует как владелец выбранной организации.
    // Автор каждого действия — он сам (sub), пометка imp видна в аудите и в ответах.
    private async Task<AccessTokenResponse> ImpersonateOrganizationAsync(AppUser user, Organization organization, CancellationToken ct)
    {
        if (!organization.IsActive)
            throw new BusinessRuleException("Организация отключена");

        var scope = new TokenScope(organization.Id, [], user.Id);
        var (token, expiresAt) = tokens.CreateAccessToken(user, [RoleNames.Owner], scope);
        audit.Record(nameof(Organization), organization.Id, "ImpersonationStarted", null,
            new { ImpersonatedBy = user.Id, user.Email }, organization.Id);
        await db.SaveChangesAsync(ct);

        logger.LogWarning("Создатель платформы {UserId} вошёл как владелец организации {OrganizationId}", user.Id, organization.Id);
        return new AccessTokenResponse(token, expiresAt,
            await auth.BuildUserResponseAsync(user, [RoleNames.Owner], scope, ct));
    }

    public async Task<AccessTokenResponse> StopImpersonationAsync(CancellationToken ct = default)
    {
        if (currentUser.ImpersonatorId is not { } impersonator || currentUser.UserId is not { } subject)
            throw new BusinessRuleException("Режим «войти как владелец» не включён");

        var user = await userManager.FindByIdAsync(impersonator.ToString())
            ?? throw new UnauthorizedException("Требуется вход");
        // Вход «как владелец» — запись по организации; вход под пользователем — по нему.
        if (subject == impersonator && currentUser.OrganizationId is { } organizationId)
            audit.Record(nameof(Organization), organizationId, "ImpersonationStopped", null, new { ImpersonatedBy = user.Id });
        else
            audit.Record(nameof(AppUser), subject, "ImpersonationStopped", null, new { ImpersonatedBy = user.Id });
        await db.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(user);
        var scope = await auth.ScopeOfAsync(user, ct);
        var (token, expiresAt) = tokens.CreateAccessToken(user, roles, scope);
        return new AccessTokenResponse(token, expiresAt, await auth.BuildUserResponseAsync(user, roles, scope, ct));
    }

    private async Task<AppUser> RequireSuperAdminAsync()
    {
        // В режиме «войти как владелец» роли SuperAdmin в токене нет — эти действия недоступны.
        if (!currentUser.IsInRole(RoleNames.SuperAdmin) || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Доступно только создателю платформы");
        return await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Требуется вход");
    }
}
