using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

// Покупатель входит по почте и своему 6-значному коду (PIN). Код хранится как пароль Identity (PasswordHash,
// стандартный хешер); правила кода — PinAwarePasswordValidator. Регистрация и «Забыли код?» подтверждаются
// одноразовым кодом из письма (EmailCode: хеш, 10 минут, 5 попыток, не чаще раза в минуту).
// Неверный код входа и незнакомая почта отвечают одинаково; 5 ошибок — блокировка Identity на 15 минут.
public partial class AuthService
{
    public const string InvalidPinPrefix = "Неверная почта или код входа. Осталось попыток: ";
    public const string PinLockedPrefix = "Вход по коду временно заблокирован. Минут до разблокировки: ";
    public const string PinNotSet = "Код входа ещё не задан. Нажмите «Забыли код?» — мы пришлём письмо, и вы зададите свой код";
    public const string PinNotSetOnRegister = "Эта почта уже есть в кабинете — задайте код входа через «Забыли код?»";
    public const string EmailTaken = "Почта уже зарегистрирована — войдите или восстановите код";
    public const string EmailNotConfirmed = "Почта не подтверждена. Введите код из письма или запросите новый";
    public const string NameRequired = "Укажите имя (от 2 до 200 символов)";

    // Назначение кода из письма входит в его хеш: код регистрации не годится для сброса и наоборот.
    private enum CodePurpose { Register, PinReset }

    private static bool Tajik => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tg";

    // ---------- Регистрация ----------

    // Новый покупатель (или ещё не подтвердивший почту — тогда имя и код обновляются): карточка клиента у застройщика,
    // учётная запись без подтверждённой почты с кодом входа и письмо с кодом подтверждения.
    public async Task<EmailSendResponse> RegisterCustomerAsync(CustomerRegisterRequest request, string? requestIp, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        if (!channels.EmailWorks)
            throw new ServiceUnavailableException(LoginChannels.EmailNotConfigured);
        var fullName = (request.Name ?? "").Trim();
        if (fullName.Length is < 2 or > 200)
            throw new BusinessRuleException(NameRequired);
        // Телефон нужен карточке клиента: по нему менеджер находит покупателя и его договоры.
        var phone = PhoneNumber.Normalize(request.Phone) ?? throw new BusinessRuleException("Неверный номер телефона");
        if (PinRules.Check(request.Pin) is { } pinError)
            throw new BusinessRuleException(pinError);

        var user = await userManager.FindByEmailAsync(address);
        if (user is not null)
        {
            if (await IsStaffAsync(user))
                throw new ConflictException("Эта почта принадлежит сотруднику. Войдите на вкладке «Сотрудник»");
            if (user.EmailConfirmed)
                throw new ConflictException(await userManager.HasPasswordAsync(user) ? EmailTaken : PinNotSetOnRegister);
        }

        // Лимиты кодов — до того, как появится карточка: иначе регистрацией можно было бы заваливать базу.
        await EnsureCodeLimitsAsync(address, requestIp, ct);

        var organizationId = await ResolveRegistrationOrganizationAsync(request.Organization, ct);
        var card = await db.Customers.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.OrganizationId == organizationId && (c.Email == address || c.Phone == phone), ct);
        // Телефон из чужой карточки не привязывается к новой почте: иначе по одному номеру можно было бы
        // получить доступ к чужим сделкам.
        if (card is not null && card.Email != address)
            throw new ConflictException(PhoneTaken);
        if (card is null)
        {
            card = new Customer { OrganizationId = organizationId, FullName = fullName, Phone = phone, Email = address };
            db.Customers.Add(card);
            audit.Record(nameof(Customer), card.Id, "Registered", null, new { card.FullName, card.Phone, card.Email }, organizationId);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException e) when (e.IsUniqueViolation())
            {
                throw new ConflictException(PhoneTaken);
            }
        }

        var repeated = user is not null;
        if (user is null)
        {
            // Без пароля, затем роль, затем код: проверяющий пароли видит роль Customer и применяет правила кода.
            user = new AppUser { UserName = address, Email = address, EmailConfirmed = false, FullName = fullName };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
                throw new ConflictException("Не удалось создать учётную запись покупателя: " +
                                            string.Join("; ", created.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(user, RoleNames.Customer);
        }
        else if (user.FullName != fullName)
        {
            user.FullName = fullName;
            await userManager.UpdateAsync(user);
        }

        await ReplacePinAsync(user, request.Pin);
        audit.Record(nameof(AppUser), user.Id, "Registered", null, new { user.Email, user.FullName, Repeated = repeated }, organizationId);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Покупатель {UserId} зарегистрировался, ждём подтверждения почты", user.Id);

        return await SendCodeAsync(CodePurpose.Register, address, requestIp, deliver: true, ct);
    }

    // Новый код подтверждения: ответ одинаковый, письмо — только если регистрация с этой почтой ждёт подтверждения.
    public async Task<EmailSendResponse> ResendConfirmationAsync(CustomerEmailRequest request, string? requestIp, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        var user = await userManager.FindByEmailAsync(address);
        var pending = user is { EmailConfirmed: false, IsActive: true }
                      && !await IsStaffAsync(user) && await userManager.HasPasswordAsync(user);
        return await SendCodeAsync(CodePurpose.Register, address, requestIp, pending, ct);
    }

    // Код из письма верный — почта подтверждена, карточки клиента с этой почтой привязаны, выдаются токены.
    public async Task<AuthResponse> ConfirmCustomerAsync(CustomerConfirmRequest request, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new UnauthorizedException(InvalidEmailCode);
        var user = await userManager.FindByEmailAsync(address);
        if (user is null || user.EmailConfirmed || await IsStaffAsync(user))
            throw new UnauthorizedException(InvalidEmailCode);

        await VerifyCodeAsync(CodePurpose.Register, address, request.Code, ct);
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        await LinkCustomerCardsAsync(user, address, ct);
        await EnsureCanSignInAsync(user, ct);

        audit.Record(nameof(AppUser), user.Id, "EmailConfirmed", null, new { user.Email }, user.OrganizationId);
        audit.Record(nameof(AppUser), user.Id, "Login", null, new { Method = "Registration" }, user.OrganizationId);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Покупатель {UserId} подтвердил почту", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // ---------- Вход ----------

    public async Task<AuthResponse> CustomerLoginAsync(CustomerLoginRequest request, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? "";
        var pin = request.Pin ?? "";
        var user = address.Length == 0 ? null : await userManager.FindByEmailAsync(address);

        // Нет учётной записи, сотрудник или отключённая — ответ и задержка как у неверного кода.
        if (user is null || !user.IsActive || await IsStaffAsync(user))
        {
            // Покупатель, которого завёл менеджер (почта есть в карточке, учётной записи ещё нет), задаёт код через «Забыли код?».
            if (user is null && address.Length > 0 && await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == address, ct))
                throw new ConflictException(PinNotSet);
            throw await UnknownPinFailureAsync(address, pin, ct);
        }

        if (!await userManager.HasPasswordAsync(user))
            throw new ConflictException(PinNotSet);

        if (await userManager.IsLockedOutAsync(user))
            throw PinLocked(await userManager.GetLockoutEndDateAsync(user));

        if (!await userManager.CheckPasswordAsync(user, pin))
        {
            await userManager.AccessFailedAsync(user);
            var locked = await userManager.IsLockedOutAsync(user);
            audit.Record(nameof(AppUser), user.Id, locked ? "PinLockedOut" : "LoginFailed", null, new { Method = "Pin" }, user.OrganizationId);
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Неверный код входа покупателя {UserId}", user.Id);
            if (locked)
                throw PinLocked(await userManager.GetLockoutEndDateAsync(user));
            throw InvalidPin(identityOptions.Value.Lockout.MaxFailedAccessAttempts - await userManager.GetAccessFailedCountAsync(user));
        }

        // О неподтверждённой почте говорим только тому, кто знает код: иначе по ответу было бы видно, что почта есть.
        if (!user.EmailConfirmed)
            throw new ForbiddenException(EmailNotConfirmed);

        await EnsureCanSignInAsync(user, ct);
        await userManager.ResetAccessFailedCountAsync(user);
        audit.Record(nameof(AppUser), user.Id, "Login", null, new { Method = "Pin" }, user.OrganizationId);
        logger.LogInformation("Покупатель {UserId} вошёл по коду", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // Незнакомая почта: тот же счётчик и та же блокировка, что Identity ведёт для настоящей учётной записи,
    // и та же проверка хеша — по ответу и по времени не понять, есть ли покупатель с такой почтой.
    private async Task<DomainException> UnknownPinFailureAsync(string address, string pin, CancellationToken ct)
    {
        userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash.Value, pin);

        var lockout = identityOptions.Value.Lockout;
        var key = "pin-failures:" + address.ToUpperInvariant();
        var failures = await ReadCachedAsync<LoginFailures>(key, ct);
        if (failures is null || failures.ExpiresAt <= Now)
            failures = new LoginFailures(0, Now.Add(lockout.DefaultLockoutTimeSpan));
        if (failures.Count >= lockout.MaxFailedAccessAttempts)
            return PinLocked(failures.ExpiresAt);

        var count = failures.Count + 1;
        if (count >= lockout.MaxFailedAccessAttempts)
        {
            // Как у Identity: блокировка — на 15 минут от последней ошибки.
            var until = Now.Add(lockout.DefaultLockoutTimeSpan);
            await SaveCachedAsync(key, new LoginFailures(count, until));
            return PinLocked(until);
        }

        await SaveCachedAsync(key, failures with { Count = count });
        return InvalidPin(lockout.MaxFailedAccessAttempts - count);
    }

    private static UnauthorizedException InvalidPin(int attemptsLeft)
    {
        var e = new UnauthorizedException(InvalidPinPrefix + attemptsLeft);
        e.Extensions["attemptsLeft"] = attemptsLeft;
        return e;
    }

    private TooManyRequestsException PinLocked(DateTimeOffset? lockoutEnd) =>
        PinLocked(lockoutEnd?.UtcDateTime ?? Now.Add(identityOptions.Value.Lockout.DefaultLockoutTimeSpan));

    private TooManyRequestsException PinLocked(DateTime until)
    {
        var seconds = Math.Max(1, (int)Math.Ceiling((until - Now).TotalSeconds));
        var e = new TooManyRequestsException(PinLockedPrefix + (int)Math.Ceiling(seconds / 60.0));
        e.Extensions["lockedUntil"] = DateTime.SpecifyKind(until, DateTimeKind.Utc);
        e.Extensions["retryAfterSeconds"] = seconds;
        return e;
    }

    // ---------- «Забыли код?» ----------

    // Ответ одинаковый для любой почты; письмо — только покупателю (есть учётная запись или карточка у застройщика).
    public async Task<EmailSendResponse> RequestPinResetAsync(CustomerEmailRequest request, string? requestIp, CancellationToken ct = default)
    {
        var address = EmailAddress.Normalize(request.Email) ?? throw new BusinessRuleException("Неверный адрес почты");
        var user = await userManager.FindByEmailAsync(address);
        var known = user is not null
            ? user.IsActive && !await IsStaffAsync(user)
            : await db.Customers.IgnoreQueryFilters().AnyAsync(c => c.Email == address, ct);
        return await SendCodeAsync(CodePurpose.PinReset, address, requestIp, known, ct);
    }

    // Новый код входа по коду из письма: блокировка снимается, все прежние сессии закрываются, выдаются новые токены.
    // Это же — первая установка кода для покупателя, которого завёл менеджер.
    public async Task<AuthResponse> ResetPinAsync(CustomerPinResetRequest request, CancellationToken ct = default)
    {
        // Слабый код — до проверки кода из письма, чтобы не тратить попытку.
        if (PinRules.Check(request.NewPin) is { } pinError)
            throw new BusinessRuleException(pinError);
        var address = EmailAddress.Normalize(request.Email) ?? throw new UnauthorizedException(InvalidEmailCode);
        var user = await userManager.FindByEmailAsync(address);
        if (user is not null && await IsStaffAsync(user))
            throw new UnauthorizedException(InvalidEmailCode);

        await VerifyCodeAsync(CodePurpose.PinReset, address, request.Code, ct);
        user ??= await GetOrCreateCustomerUserByEmailAsync(address, ct);
        await EnsureCanSignInAsync(user, ct);

        await ReplacePinAsync(user, request.NewPin);
        if (!user.EmailConfirmed)
        {
            // Код из письма доказывает, что почта своя.
            user.EmailConfirmed = true;
            await userManager.UpdateAsync(user);
        }
        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        await LinkCustomerCardsAsync(user, address, ct);

        var now = Now;
        audit.Record(nameof(AppUser), user.Id, "PinReset", null, null, user.OrganizationId);
        await db.SaveChangesAsync(ct);
        await RevokeAllAsync(user.Id, now, ct);
        logger.LogInformation("Покупатель {UserId} задал новый код входа", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // ---------- Общее ----------

    // Правила кода уже проверены (PinRules), поэтому между удалением старого и записью нового ошибки быть не должно.
    private async Task ReplacePinAsync(AppUser user, string pin)
    {
        if (await userManager.HasPasswordAsync(user))
            await userManager.RemovePasswordAsync(user);
        var result = await userManager.AddPasswordAsync(user, pin);
        if (!result.Succeeded)
            throw new BusinessRuleException(result.Errors.Select(e => e.Description).FirstOrDefault() ?? PinRules.FormatMessage);
    }

    // Карточки клиента с этой почтой, ещё ни к кому не привязанные, — к учётной записи покупателя.
    private async Task LinkCustomerCardsAsync(AppUser user, string address, CancellationToken ct)
    {
        var cards = await db.Customers.IgnoreQueryFilters().Where(c => c.Email == address && c.UserId == null).ToListAsync(ct);
        cards.ForEach(c => c.UserId = user.Id);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCodeLimitsAsync(string address, string? requestIp, CancellationToken ct)
    {
        var now = Now;
        await EnsureEmailCodeLimitAsync(c => c.Email == address, now, authOptions.Value, ct);
        if (!string.IsNullOrEmpty(requestIp))
            await EnsureEmailCodeLimitAsync(c => c.RequestIp == requestIp, now, authOptions.Value, ct);
    }

    // Код создаётся и учитывается в лимитах всегда, а письмо уходит, только если deliver: так ответ и лимиты
    // одинаковы для любой почты.
    private async Task<EmailSendResponse> SendCodeAsync(CodePurpose purpose, string address, string? requestIp, bool deliver, CancellationToken ct)
    {
        if (!channels.EmailWorks)
            throw new ServiceUnavailableException(LoginChannels.EmailNotConfigured);
        await EnsureCodeLimitsAsync(address, requestIp, ct);

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        if (IsDevFixedCodeEmail(address))
            logger.LogInformation("Код из письма для {Email}: {Code} (разработка; подходит и {FixedCode})", address, code, AuthOptions.DevFixedCode);
        db.EmailCodes.Add(new EmailCode
        {
            Email = address,
            CodeHash = HashCode(CodeTarget(purpose, address), code),
            RequestIp = requestIp,
            Sent = deliver,
            ExpiresAt = Now.Add(EmailCode.Lifetime)
        });
        await db.SaveChangesAsync(ct);

        if (deliver)
        {
            var brand = await brands.NameForEmailAsync(address, ct);
            // Сбой почтового сервиса не меняет ответ: иначе по ошибке 500 было бы видно, что почта зарегистрирована.
            try
            {
                // Отправитель — застройщик покупателя («Некруз Резиденс <no-reply@…>»), а не платформа.
                await email.SendAsync(CodeMessage(purpose, address, brand, code) with { FromName = brand }, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Письмо с кодом не отправлено: ошибка почтового сервиса");
            }
        }
        else
            logger.LogInformation("Код не отправлен: почта не найдена среди покупателей");

        var message = purpose == CodePurpose.Register
            ? "Мы отправили код подтверждения на почту"
            : "Если почта зарегистрирована, мы отправили на неё код для сброса";
        return new EmailSendResponse(message, (int)EmailCode.Lifetime.TotalSeconds, 60);
    }

    private static EmailMessage CodeMessage(CodePurpose purpose, string address, string brand, string code)
    {
        var minutes = EmailCode.Lifetime.TotalMinutes.ToString("0", CultureInfo.InvariantCulture);
        return (purpose, Tajik) switch
        {
            (CodePurpose.Register, false) => new EmailMessage(address, $"{brand}: код подтверждения регистрации {code}",
                $"Код подтверждения регистрации в {brand}: {code}\n\nКод действует {minutes} минут. " +
                "Никому не сообщайте код — сотрудники его никогда не спрашивают. " +
                "Если вы не регистрировались, просто удалите это письмо."),
            (CodePurpose.Register, true) => new EmailMessage(address, $"{brand}: рамзи тасдиқи бақайдгирӣ {code}",
                $"Рамзи тасдиқи бақайдгирӣ дар {brand}: {code}\n\nРамз {minutes} дақиқа амал мекунад. " +
                "Рамзро ба ҳеҷ кас нагӯед — кормандон онро ҳеҷ гоҳ намепурсанд. " +
                "Агар шумо бақайд нагирифта бошед, ин мактубро нест кунед."),
            (_, false) => new EmailMessage(address, $"{brand}: код для сброса кода входа {code}",
                $"Код для сброса кода входа в {brand}: {code}\n\nКод действует {minutes} минут. " +
                "Никому не сообщайте код — сотрудники его никогда не спрашивают. " +
                "Если вы не просили сбросить код входа, просто удалите это письмо: прежний код продолжает работать."),
            (_, true) => new EmailMessage(address, $"{brand}: рамз барои иваз кардани рамзи воридшавӣ {code}",
                $"Рамз барои иваз кардани рамзи воридшавӣ дар {brand}: {code}\n\nРамз {minutes} дақиқа амал мекунад. " +
                "Рамзро ба ҳеҷ кас нагӯед — кормандон онро ҳеҷ гоҳ намепурсанд. " +
                "Агар шумо иваз кардани рамзро нахоста бошед, ин мактубро нест кунед: рамзи пештара амал мекунад.")
        };
    }

    // Действует только последний код почты: запрос нового делает прежние бесполезными.
    // Разработка: подходит и 123456 (Auth:DevFixedCodeForAnyEmail или почта из Auth:DevFixedCodeEmails).
    private async Task VerifyCodeAsync(CodePurpose purpose, string address, string? code, CancellationToken ct)
    {
        var now = Now;
        var entered = (code ?? "").Trim();
        var stored = await db.EmailCodes.Where(c => c.Email == address).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(ct);

        if (IsDevFixedCodeEmail(address) && entered == AuthOptions.DevFixedCode)
        {
            if (stored is not null)
                stored.UsedAt = now;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (stored is null || !stored.IsUsable(now))
            throw new UnauthorizedException(InvalidEmailCode);
        if (!FixedTimeEquals(stored.CodeHash, HashCode(CodeTarget(purpose, address), entered)))
        {
            stored.Attempts++;
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidEmailCode);
        }

        stored.UsedAt = now;
        await db.SaveChangesAsync(ct);
    }

    private static string CodeTarget(CodePurpose purpose, string address) =>
        (purpose == CodePurpose.Register ? "register:" : "pin-reset:") + address;
}
