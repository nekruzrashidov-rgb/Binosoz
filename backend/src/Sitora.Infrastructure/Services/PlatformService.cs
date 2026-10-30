using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

// Действия создателя платформы: организации, вход «как владелец» и второй фактор.
public class PlatformService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    AuthService auth,
    JwtTokenService tokens,
    ICurrentUser currentUser,
    IAuditService audit,
    IEmailSender email,
    IOptions<EmailOptions> emailOptions,
    ILogger<PlatformService> logger) : IPlatformService
{
    private const string AuthenticatorIssuer = "Sitora";

    public async Task<IReadOnlyList<OrganizationResponse>> GetOrganizationsAsync(CancellationToken ct = default) =>
        await db.Organizations.AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationResponse(o.Id, o.Name, o.Slug, o.IsActive,
                o.Buildings.Count, db.Users.Count(u => u.OrganizationId == o.Id), o.CreatedAt, o.CustomDomain))
            .ToListAsync(ct);

    // Новая организация сразу получает владельца; он входит по коду из почты.
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

        var organization = new Organization { Name = request.Name.Trim(), Slug = slug };
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(ct);

        var owner = new AppUser
        {
            UserName = ownerEmail,
            Email = ownerEmail,
            EmailConfirmed = true,
            FullName = request.OwnerFullName.Trim(),
            OrganizationId = organization.Id
        };
        var result = await userManager.CreateAsync(owner);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(owner, RoleNames.Owner);

        audit.Record(nameof(Organization), organization.Id, "Created", null,
            new { organization.Name, organization.Slug, OwnerEmail = ownerEmail }, organization.Id);
        await db.SaveChangesAsync(ct);

        // Организация уже сохранена, а владелец входит по коду и без этого письма: сбой почты не отменяет создание.
        try
        {
            await email.SendAsync(new EmailMessage(ownerEmail, $"Sitora: вы владелец «{organization.Name}»",
                $"Здравствуйте, {owner.FullName}!\n\nДля вас создана организация «{organization.Name}» на платформе Sitora. " +
                $"Входите по коду, который придёт на эту почту: {emailOptions.Value.InviteUrl.Split('#')[0]}"), ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось отправить письмо владельцу {Email}", ownerEmail);
        }

        logger.LogInformation("Создана организация {OrganizationId} ({Slug})", organization.Id, slug);
        return new OrganizationResponse(organization.Id, organization.Name, organization.Slug, organization.IsActive, 0, 1,
            organization.CreatedAt);
    }

    // Ещё один владелец организации. Пароль необязателен: владелец входит по коду из почты.
    public async Task<UserResponse> AddOwnerAsync(Guid organizationId, AddOwnerRequest request, CancellationToken ct = default)
    {
        await RequirePlatformOwnerAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
        var email = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты владельца");
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName))
            throw new BusinessRuleException("Укажите имя владельца");
        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        var owner = new AppUser { UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, OrganizationId = organization.Id };
        var result = string.IsNullOrEmpty(request.Password)
            ? await userManager.CreateAsync(owner)
            : await userManager.CreateAsync(owner, request.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(owner, RoleNames.Owner);

        audit.Record(nameof(AppUser), owner.Id, "OwnerAdded", null, new { owner.Email, owner.FullName, OrganizationId = organization.Id }, organization.Id);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("В организацию {OrganizationId} добавлен владелец {UserId}", organization.Id, owner.Id);
        return new UserResponse(owner.Id, owner.Email, owner.PhoneNumber, owner.FullName, [RoleNames.Owner], owner.IsActive, owner.CreatedAt,
            organization.Id, []);
    }

    // id — организация или пользователь. Пометка imp видна в аудите и в каждом ответе (X-Impersonated-By).
    public async Task<AccessTokenResponse> ImpersonateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await RequirePlatformOwnerAsync();
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
        if (roles.Contains(RoleNames.PlatformOwner))
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

    // Ключ для приложения-аутентификатора (Google Authenticator и т. п.). Включается кодом из приложения.
    public async Task<TotpSetupResponse> GetTotpSetupAsync(CancellationToken ct = default)
    {
        var user = await RequirePlatformOwnerAsync();
        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var account = Uri.EscapeDataString(user.Email ?? user.Id.ToString());
        var uri = $"otpauth://totp/{AuthenticatorIssuer}:{account}?secret={key}&issuer={AuthenticatorIssuer}&digits=6";
        return new TotpSetupResponse(key!, uri);
    }

    public async Task EnableTotpAsync(TotpEnableRequest request, CancellationToken ct = default)
    {
        var user = await RequirePlatformOwnerAsync();
        var code = request.Code.Replace(" ", "", StringComparison.Ordinal);
        if (!await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code))
            throw new BusinessRuleException("Неверный код из приложения");

        await userManager.SetTwoFactorEnabledAsync(user, true);
        audit.Record(nameof(AppUser), user.Id, "TotpEnabled", null, null);
        await db.SaveChangesAsync(ct);
    }

    private async Task<AppUser> RequirePlatformOwnerAsync()
    {
        // В режиме «войти как владелец» роли PlatformOwner в токене нет — эти действия недоступны.
        if (!currentUser.IsInRole(RoleNames.PlatformOwner) || currentUser.UserId is not { } userId)
            throw new ForbiddenException("Доступно только создателю платформы");
        return await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Требуется вход");
    }
}
