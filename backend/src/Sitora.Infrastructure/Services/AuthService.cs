using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    JwtTokenService tokens,
    ISmsSender sms,
    IEmailSender email,
    IBrandService brands,
    IAuditService audit,
    ICurrentUser currentUser,
    IMemoryCache cache,
    IOptions<IdentityOptions> identityOptions,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Неверная почта или пароль";
    private const string LockedOut = "Слишком много неудачных попыток входа, попробуйте позже";
    private const string InvalidRefreshToken = "Сессия истекла, войдите заново";
    private const string InvalidSmsCode = "Неверный или устаревший код";
    private const string InvalidEmailCode = "Неверный или устаревший код";
    private const string InvalidChallenge = "Время на подтверждение входа истекло, запросите код заново";
    private const string InvalidInvitation = "Приглашение недействительно или уже использовано";
    public const int MaxSmsCodesPerHour = 5;

    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);
    private const int MaxChallengeAttempts = 5;

    // ---------- Вход по почте ----------

    // Ответ одинаковый, есть ли такая учётная запись: код создаётся и учитывается в лимитах всегда,
    // а письмо уходит, только если почта принадлежит сотруднику или покупателю.
    public async Task<EmailSendResponse> SendEmailCodeAsync(EmailSendRequest request, string? requestIp, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email)
            ?? throw new BusinessRuleException("Неверный адрес почты");
        var now = Now;
        var limits = authOptions.Value;

        await EnsureEmailCodeLimitAsync(c => c.Email == address, now, limits, ct);
        if (!string.IsNullOrEmpty(requestIp))
            await EnsureEmailCodeLimitAsync(c => c.RequestIp == requestIp, now, limits, ct);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var known = await IsKnownEmailAsync(address, ct);
        db.EmailCodes.Add(new EmailCode
        {
            Email = address,
            CodeHash = HashCode(address, code),
            RequestIp = requestIp,
            Sent = known,
            ExpiresAt = now.Add(EmailCode.Lifetime)
        });
        await db.SaveChangesAsync(ct);

        // Письмо подписано брендом застройщика: покупатель знает его, а не платформу.
        if (known)
        {
            var brand = await brands.NameForEmailAsync(address, ct);
            // Сбой почтового сервиса не меняет ответ: иначе по ошибке 500 было бы видно, что почта зарегистрирована.
            try
            {
                await email.SendAsync(new EmailMessage(address, $"{brand}: код для входа {code}",
                    $"Ваш код для входа в {brand}: {code}\n\nКод действует {EmailCode.Lifetime.TotalMinutes:0} минут. " +
                    "Никому его не сообщайте. Если вы не запрашивали код, просто удалите это письмо."), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Письмо с кодом входа не отправлено: ошибка почтового сервиса");
            }
        }
        else
            logger.LogInformation("Код не отправлен: почта не найдена среди пользователей и покупателей");

        return new EmailSendResponse("Если почта зарегистрирована, мы отправили на неё код для входа",
            (int)EmailCode.Lifetime.TotalSeconds);
    }

    private async Task EnsureEmailCodeLimitAsync(System.Linq.Expressions.Expression<Func<EmailCode, bool>> who, DateTime now,
        AuthOptions limits, CancellationToken ct)
    {
        var recent = db.EmailCodes.Where(who);
        if (await recent.CountAsync(c => c.CreatedAt > now.AddMinutes(-1), ct) >= limits.EmailCodesPerMinute)
            throw new TooManyRequestsException("Код уже отправлен. Запросить новый можно через минуту");
        if (await recent.CountAsync(c => c.CreatedAt > now.AddHours(-1), ct) >= limits.EmailCodesPerHour)
            throw new TooManyRequestsException("Слишком много запросов кода. Попробуйте позже");
    }

    private async Task<bool> IsKnownEmailAsync(string address, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(address);
        if (user is not null)
            return user.IsActive;
        return await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == address, ct);
    }

    public async Task<EmailLoginResult> VerifyEmailCodeAsync(EmailVerifyRequest request, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new UnauthorizedException(InvalidEmailCode);
        var now = Now;

        // Действует только последний код: запрос нового делает прежние бесполезными.
        var stored = await db.EmailCodes
            .Where(c => c.Email == address)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (stored is null || !stored.IsUsable(now))
            throw new UnauthorizedException(InvalidEmailCode);

        if (!FixedTimeEquals(stored.CodeHash, HashCode(address, request.Code.Trim())))
        {
            stored.Attempts++;
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidEmailCode);
        }

        stored.UsedAt = now;
        await db.SaveChangesAsync(ct);

        var user = await userManager.FindByEmailAsync(address) ?? await GetOrCreateCustomerUserByEmailAsync(address, ct);
        await EnsureCanSignInAsync(user, ct);

        if (await userManager.IsInRoleAsync(user, RoleNames.PlatformOwner))
            return new EmailLoginResult(null, await CreateChallengeAsync(user));

        logger.LogInformation("Пользователь {UserId} вошёл по коду из почты", user.Id);
        return new EmailLoginResult(await IssueTokensAsync(user, ct), null);
    }

    // Покупатель сам регистрируется на сайте застройщика: появляется карточка клиента с этой почтой,
    // и на почту уходит код — как при обычном входе. Учётная запись создаётся при вводе кода.
    public async Task<EmailSendResponse> RegisterCustomerAsync(RegisterCustomerRequest request, string? requestIp, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        var phone = PhoneNumber.Normalize(request.Phone) ?? throw new BusinessRuleException("Неверный номер телефона");
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrEmpty(fullName) || fullName.Length > 200)
            throw new BusinessRuleException("Укажите имя (до 200 символов)");

        // Лимиты кодов проверяются до того, как появится карточка: иначе регистрацией можно было бы заваливать базу.
        var limits = authOptions.Value;
        await EnsureEmailCodeLimitAsync(c => c.Email == address, Now, limits, ct);
        if (!string.IsNullOrEmpty(requestIp))
            await EnsureEmailCodeLimitAsync(c => c.RequestIp == requestIp, Now, limits, ct);

        var organizationId = await ResolveRegistrationOrganizationAsync(request.Organization, ct);
        var existing = await db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && (c.Email == address || c.Phone == phone), ct);
        if (existing is null)
        {
            var customer = new Customer { OrganizationId = organizationId, FullName = fullName, Phone = phone, Email = address };
            db.Customers.Add(customer);
            audit.Record(nameof(Customer), customer.Id, "Registered", null,
                new { customer.FullName, customer.Phone, customer.Email }, organizationId);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.IsUniqueViolation())
            {
                throw new ConflictException(PhoneTaken);
            }
        }
        // Телефон из чужой карточки не привязывается к новой почте: иначе по одному номеру можно было бы
        // получить доступ к чужим сделкам.
        else if (existing.Email != address)
            throw new ConflictException(PhoneTaken);

        return await SendEmailCodeAsync(new EmailSendRequest(address), requestIp, ct);
    }

    private const string PhoneTaken =
        "Этот телефон уже указан в договоре с другой почтой. Войдите по почте из договора или обратитесь к менеджеру";

    private async Task<Guid> ResolveRegistrationOrganizationAsync(string? slug, CancellationToken ct)
    {
        var active = db.Organizations.IgnoreQueryFilters().Where(o => o.IsActive);
        if (!string.IsNullOrWhiteSpace(slug))
        {
            var normalized = BrandRules.NormalizeSlug(slug);
            return await active.Where(o => o.Slug == normalized).Select(o => (Guid?)o.Id).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Застройщик не найден");
        }

        var ids = await active.Select(o => o.Id).Take(2).ToListAsync(ct);
        return ids is [var single] ? single : throw new BusinessRuleException("Укажите застройщика (organization)");
    }

    // Отдельный вход создателя платформы: только для этой роли, дальше — второй фактор, как и в обычном входе.
    // Для чужой почты ответ такой же, как на неверный код, и код при этом не тратится.
    public async Task<SecondFactorChallenge> VerifyPlatformCodeAsync(EmailVerifyRequest request, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new UnauthorizedException(InvalidEmailCode);
        var user = await userManager.FindByEmailAsync(address);
        if (user is null || !await userManager.IsInRoleAsync(user, RoleNames.PlatformOwner))
            throw new UnauthorizedException(InvalidEmailCode);

        var result = await VerifyEmailCodeAsync(request, ct);
        return result.Challenge ?? throw new UnauthorizedException(InvalidEmailCode);
    }

    // Покупатель входит по почте из своих сделок: учётная запись создаётся при первом входе
    // и привязывается ко всем записям клиента с этой почтой (у разных застройщиков).
    private async Task<AppUser> GetOrCreateCustomerUserByEmailAsync(string address, CancellationToken ct)
    {
        var customers = await db.Customers.IgnoreQueryFilters().Where(c => c.Email == address).ToListAsync(ct);
        if (customers.Count == 0)
            throw new UnauthorizedException(InvalidEmailCode);

        AppUser? user = null;
        foreach (var userId in customers.Where(c => c.UserId != null).Select(c => c.UserId!.Value).Distinct())
        {
            user = await userManager.FindByIdAsync(userId.ToString());
            if (user is not null)
                break;
        }

        if (user is null)
        {
            user = new AppUser { UserName = address, Email = address, EmailConfirmed = true, FullName = customers[0].FullName };
            var result = await userManager.CreateAsync(user);
            if (!result.Succeeded)
                throw new ConflictException("Не удалось создать учётную запись покупателя: " +
                                            string.Join("; ", result.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, RoleNames.Customer);
        }
        else if (user.Email is null)
        {
            // Покупатель раньше входил по SMS: теперь у учётной записи появляется почта.
            await userManager.SetEmailAsync(user, address);
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }

        foreach (var customer in customers)
            customer.UserId = user.Id;
        await db.SaveChangesAsync(ct);
        return user;
    }

    // ---------- Второй фактор (создатель платформы) ----------

    private async Task<SecondFactorChallenge> CreateChallengeAsync(AppUser user)
    {
        var methods = new List<string>();
        if (await userManager.HasPasswordAsync(user))
            methods.Add("password");
        if (await userManager.GetTwoFactorEnabledAsync(user))
            methods.Add("totp");
        if (methods.Count == 0)
            throw new UnauthorizedException("Для этой учётной записи не настроен второй фактор. Задайте пароль в ADMIN_PASSWORD");

        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expiresAt = Now.Add(ChallengeLifetime);
        cache.Set(ChallengeKey(token), new StrongBox<(Guid UserId, int Attempts)>((user.Id, 0)), ChallengeLifetime);
        return new SecondFactorChallenge(token, expiresAt, methods,
            "Код принят. Подтвердите вход паролем или кодом из приложения-аутентификатора");
    }

    public async Task<AuthResponse> CompleteSecondFactorAsync(SecondFactorRequest request, CancellationToken ct = default)
    {
        var key = ChallengeKey(request.ChallengeToken);
        if (!cache.TryGetValue(key, out StrongBox<(Guid UserId, int Attempts)>? challenge) || challenge is null)
            throw new UnauthorizedException(InvalidChallenge);

        var user = await userManager.FindByIdAsync(challenge.Value.UserId.ToString());
        if (user is null || !user.IsActive || !await userManager.IsInRoleAsync(user, RoleNames.PlatformOwner))
        {
            cache.Remove(key);
            throw new UnauthorizedException(InvalidChallenge);
        }

        bool ok;
        if (!string.IsNullOrWhiteSpace(request.TotpCode) && await userManager.GetTwoFactorEnabledAsync(user))
            ok = await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider,
                request.TotpCode.Replace(" ", "", StringComparison.Ordinal));
        else if (!string.IsNullOrEmpty(request.Password))
        {
            if (await userManager.IsLockedOutAsync(user))
                throw new UnauthorizedException(LockedOut);
            ok = await userManager.CheckPasswordAsync(user, request.Password);
            if (!ok)
                await userManager.AccessFailedAsync(user);
        }
        else
            throw new BusinessRuleException("Введите пароль или код из приложения-аутентификатора");

        if (!ok)
        {
            challenge.Value = (challenge.Value.UserId, challenge.Value.Attempts + 1);
            if (challenge.Value.Attempts >= MaxChallengeAttempts)
                cache.Remove(key);
            logger.LogWarning("Неверный второй фактор создателя платформы {UserId}", user.Id);
            throw new UnauthorizedException("Неверный пароль или код");
        }

        cache.Remove(key);
        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Создатель платформы {UserId} вошёл с двумя факторами", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    private static string ChallengeKey(string token) => "2fa:" + JwtTokenService.HashToken(token);

    // ---------- Приглашение сотрудника ----------

    public async Task<AuthResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashToken(request.Token.Trim());
        var invitation = await db.Invitations.IgnoreQueryFilters().Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null || !invitation.IsPending(Now) || !invitation.Organization.IsActive)
            throw new UnauthorizedException(InvalidInvitation);
        if (await userManager.FindByEmailAsync(invitation.Email) is not null)
            throw new ConflictException("Пользователь с такой почтой уже есть");

        var user = new AppUser
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            EmailConfirmed = true,
            FullName = invitation.FullName,
            OrganizationId = invitation.OrganizationId
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new BusinessRuleException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, invitation.Role);

        foreach (var buildingId in invitation.BuildingIds)
            db.UserBuildings.Add(new UserBuilding { UserId = user.Id, BuildingId = buildingId });
        invitation.AcceptedAt = Now;
        audit.Record(nameof(Invitation), invitation.Id, "Accepted", null,
            new { invitation.Email, invitation.Role, invitation.BuildingIds, UserId = user.Id }, invitation.OrganizationId);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Принято приглашение {InvitationId}: сотрудник {UserId}", invitation.Id, user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // ---------- Старый вход по SMS ----------

    // Ответ одинаковый для любого номера, чтобы по нему нельзя было узнать, кто покупатель.
    public async Task<SmsSendResponse> SendSmsCodeAsync(SmsSendRequest request, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("Неверный номер телефона");
        var now = Now;

        var sentLastHour = await db.SmsCodes.CountAsync(c => c.Phone == phone && c.CreatedAt > now.AddHours(-1), ct);
        if (sentLastHour >= MaxSmsCodesPerHour)
            throw new TooManyRequestsException("Слишком много запросов кода. Попробуйте позже");

        if (await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Phone == phone, ct))
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            db.SmsCodes.Add(new SmsCode { Phone = phone, CodeHash = HashCode(phone, code), ExpiresAt = now.Add(SmsCode.Lifetime) });
            await db.SaveChangesAsync(ct);
            await sms.SendAsync(phone, $"Sitora: код для входа {code}. Никому его не сообщайте.", ct);
        }
        else
        {
            logger.LogInformation("Код не отправлен: номер не найден среди покупателей");
        }

        return new SmsSendResponse("Если номер есть среди покупателей, мы отправили SMS с кодом",
            (int)SmsCode.Lifetime.TotalSeconds);
    }

    public async Task<AuthResponse> VerifySmsCodeAsync(SmsVerifyRequest request, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.Phone) ?? throw new UnauthorizedException(InvalidSmsCode);
        var now = Now;

        var smsCode = await db.SmsCodes
            .Where(c => c.Phone == phone && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (smsCode is null || !smsCode.IsUsable(now))
            throw new UnauthorizedException(InvalidSmsCode);

        if (!FixedTimeEquals(smsCode.CodeHash, HashCode(phone, request.Code.Trim())))
        {
            smsCode.Attempts++;
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidSmsCode);
        }

        smsCode.UsedAt = now;
        var customers = await db.Customers.IgnoreQueryFilters().Where(c => c.Phone == phone).ToListAsync(ct);
        if (customers.Count == 0)
            throw new UnauthorizedException(InvalidSmsCode);
        var user = await GetOrCreateCustomerUserByPhoneAsync(customers);
        if (!user.IsActive)
            throw new UnauthorizedException("Учётная запись заблокирована");

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Покупатель {UserId} вошёл по SMS", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    private async Task<AppUser> GetOrCreateCustomerUserByPhoneAsync(List<Customer> customers)
    {
        foreach (var userId in customers.Where(c => c.UserId != null).Select(c => c.UserId!.Value).Distinct())
        {
            if (await userManager.FindByIdAsync(userId.ToString()) is { } existing)
            {
                customers.ForEach(c => c.UserId = existing.Id);
                return existing;
            }
        }

        var first = customers[0];
        var user = new AppUser
        {
            UserName = first.Phone,
            PhoneNumber = first.Phone,
            PhoneNumberConfirmed = true,
            FullName = first.FullName
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new ConflictException("Не удалось создать учётную запись покупателя: " +
                                        string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, RoleNames.Customer);
        customers.ForEach(c => c.UserId = user.Id);
        return user;
    }

    // ---------- Вход по паролю, обновление и выход ----------

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var address = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(address);
        // Создатель платформы по паролю не входит: только код из почты и второй фактор.
        if (user is null || !user.IsActive || await userManager.IsInRoleAsync(user, RoleNames.PlatformOwner))
            throw UnknownAccountFailure(address, request.Password);

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException(LockedOut);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Неудачный вход для пользователя {UserId}", user.Id);
            throw new UnauthorizedException(InvalidCredentials);
        }

        await EnsureCanSignInAsync(user, ct);
        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Пользователь {UserId} вошёл в систему", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // Для несуществующей или отключённой почты ведём такой же счётчик ошибок, как Identity для настоящей,
    // и так же тратим время на проверку пароля: иначе по ответу или по задержке можно узнать, кто сотрудник.
    private UnauthorizedException UnknownAccountFailure(string address, string password)
    {
        userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash.Value, password);

        var lockout = identityOptions.Value.Lockout;
        var failures = cache.GetOrCreate("login-failures:" + address.ToUpperInvariant(), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = lockout.DefaultLockoutTimeSpan;
            return new StrongBox<int>();
        })!;
        if (failures.Value >= lockout.MaxFailedAccessAttempts)
            return new UnauthorizedException(LockedOut);

        Interlocked.Increment(ref failures.Value);
        return new UnauthorizedException(InvalidCredentials);
    }

    private static readonly AppUser DummyUser = new();
    private static readonly Lazy<string> DummyPasswordHash =
        new(() => new PasswordHasher<AppUser>().HashPassword(DummyUser, Guid.NewGuid().ToString()));

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var now = Now;
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
            throw new UnauthorizedException(InvalidRefreshToken);

        // Повторное использование отозванного токена означает, что его украли: гасим все сессии пользователя.
        if (stored.RevokedAt is not null)
        {
            await RevokeAllAsync(stored.UserId, now, ct);
            logger.LogWarning("Повторное использование refresh-токена пользователя {UserId}", stored.UserId);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (!stored.IsActive(now) || !stored.User.IsActive || !await OrganizationActiveAsync(stored.User, ct))
            throw new UnauthorizedException(InvalidRefreshToken);

        return await IssueTokensAsync(stored.User, ct, stored);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null)
            return;

        stored.RevokedAt = Now;
        await db.SaveChangesAsync(ct);
    }

    // В режиме «войти как владелец» отвечаем по claims токена: роль Owner в выбранной организации.
    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Пользователь не найден");

        // «Как владелец организации»: в токене сам создатель платформы с ролью Owner.
        if (currentUser.ImpersonatorId is { } impersonator && impersonator == userId && currentUser.OrganizationId is { } organizationId)
            return await BuildUserResponseAsync(user, [RoleNames.Owner], new TokenScope(organizationId, [], impersonator), ct);

        // Под конкретным пользователем: его роли и дома, с пометкой, кто вошёл.
        var scope = await ScopeOfAsync(user, ct);
        if (currentUser.ImpersonatorId is { } asUser)
            scope = scope with { ImpersonatedBy = asUser };
        return await BuildUserResponseAsync(user, await userManager.GetRolesAsync(user), scope, ct);
    }

    // ---------- Общее ----------

    private async Task EnsureCanSignInAsync(AppUser user, CancellationToken ct)
    {
        if (!user.IsActive)
            throw new UnauthorizedException("Учётная запись заблокирована");
        if (!await OrganizationActiveAsync(user, ct))
            throw new UnauthorizedException("Организация отключена. Обратитесь к владельцу платформы");
    }

    private async Task<bool> OrganizationActiveAsync(AppUser user, CancellationToken ct) =>
        user.OrganizationId is not { } organizationId
        || await db.Organizations.IgnoreQueryFilters().AnyAsync(o => o.Id == organizationId && o.IsActive, ct);

    public async Task<TokenScope> ScopeOfAsync(AppUser user, CancellationToken ct)
    {
        var buildings = user.OrganizationId is null
            ? []
            : await db.UserBuildings.Where(ub => ub.UserId == user.Id).Select(ub => ub.BuildingId).OrderBy(id => id).ToListAsync(ct);
        return new TokenScope(user.OrganizationId, buildings);
    }

    public async Task<CurrentUserResponse> BuildUserResponseAsync(AppUser user, IList<string> roles, TokenScope scope, CancellationToken ct)
    {
        string? organizationName = null;
        if (scope.OrganizationId is { } organizationId)
            organizationName = await db.Organizations.IgnoreQueryFilters()
                .Where(o => o.Id == organizationId).Select(o => o.Name).FirstOrDefaultAsync(ct);

        return new CurrentUserResponse(user.Id, user.Email, user.PhoneNumber, user.FullName, roles.ToList(),
            PrimaryRole(roles), scope.OrganizationId, organizationName, scope.BuildingIds,
            scope.ImpersonatedBy is { } impersonator ? new ImpersonationInfo(impersonator, scope.OrganizationId) : null);
    }

    public static string? PrimaryRole(IEnumerable<string> roles) => RoleNames.All.FirstOrDefault(roles.Contains);

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var scope = await ScopeOfAsync(user, ct);
        var (accessToken, accessExpiresAt) = tokens.CreateAccessToken(user, roles, scope);
        var (refreshToken, refreshHash, refreshExpiresAt) = tokens.CreateRefreshToken();

        if (replacing is not null)
        {
            replacing.RevokedAt = Now;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpiresAt
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt,
            await BuildUserResponseAsync(user, roles, scope, ct));
    }

    private Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    private static string HashCode(string target, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{target}:{code}")));

    private static bool FixedTimeEquals(string expectedHash, string actualHash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expectedHash), Encoding.ASCII.GetBytes(actualHash));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
