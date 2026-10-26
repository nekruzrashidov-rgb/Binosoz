using System.Text.RegularExpressions;
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
public partial class PlatformService(
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
                o.Buildings.Count, db.Users.Count(u => u.OrganizationId == o.Id), o.CreatedAt))
            .ToListAsync(ct);

    // Новая организация сразу получает владельца; он входит по коду из почты.
    public async Task<OrganizationResponse> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken ct = default)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug))
            throw new BusinessRuleException("Короткое имя — латинские буквы, цифры и дефис, от 2 до 64 символов");
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

        await email.SendAsync(new EmailMessage(ownerEmail, $"Sitora: вы владелец «{organization.Name}»",
            $"Здравствуйте, {owner.FullName}!\n\nДля вас создана организация «{organization.Name}» на платформе Sitora. " +
            $"Входите по коду, который придёт на эту почту: {emailOptions.Value.InviteUrl.Split('#')[0]}"), ct);

        logger.LogInformation("Создана организация {OrganizationId} ({Slug})", organization.Id, slug);
        return new OrganizationResponse(organization.Id, organization.Name, organization.Slug, organization.IsActive, 0, 1,
            organization.CreatedAt);
    }

    // Токен без refresh: создатель платформы действует как владелец выбранной организации.
    // Автор каждого действия — он сам (sub), пометка imp видна в аудите и в ответах.
    public async Task<AccessTokenResponse> ImpersonateAsync(Guid organizationId, CancellationToken ct = default)
    {
        var user = await RequirePlatformOwnerAsync();
        var organization = await db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Организация не найдена");
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
        if (currentUser.ImpersonatorId is not { } impersonator || currentUser.OrganizationId is not { } organizationId)
            throw new BusinessRuleException("Режим «войти как владелец» не включён");

        var user = await userManager.FindByIdAsync(impersonator.ToString())
            ?? throw new UnauthorizedException("Требуется вход");
        audit.Record(nameof(Organization), organizationId, "ImpersonationStopped", null, new { ImpersonatedBy = user.Id });
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

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,62}[a-z0-9]$")]
    private static partial Regex SlugPattern();
}
