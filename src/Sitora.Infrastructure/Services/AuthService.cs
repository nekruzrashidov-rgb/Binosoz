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
    IMemoryCache cache,
    IOptions<IdentityOptions> identityOptions,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Неверная почта или пароль";
    private const string LockedOut = "Слишком много неудачных попыток входа, попробуйте позже";
    private const string InvalidRefreshToken = "Сессия истекла, войдите заново";
    private const string InvalidSmsCode = "Неверный или устаревший код";
    public const int MaxSmsCodesPerHour = 5;

    // Ответ одинаковый для любого номера, чтобы по нему нельзя было узнать, кто покупатель.
    public async Task<SmsSendResponse> SendSmsCodeAsync(SmsSendRequest request, CancellationToken ct = default)
    {
        var phone = PhoneNumber.Normalize(request.Phone)
            ?? throw new BusinessRuleException("Неверный номер телефона");
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var sentLastHour = await db.SmsCodes.CountAsync(c => c.Phone == phone && c.CreatedAt > now.AddHours(-1), ct);
        if (sentLastHour >= MaxSmsCodesPerHour)
            throw new TooManyRequestsException("Слишком много запросов кода. Попробуйте позже");

        if (await db.Customers.AnyAsync(c => c.Phone == phone, ct))
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            db.SmsCodes.Add(new SmsCode { Phone = phone, CodeHash = HashSmsCode(phone, code), ExpiresAt = now.Add(SmsCode.Lifetime) });
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
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var smsCode = await db.SmsCodes
            .Where(c => c.Phone == phone && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (smsCode is null || !smsCode.IsUsable(now))
            throw new UnauthorizedException(InvalidSmsCode);

        var expected = Encoding.ASCII.GetBytes(smsCode.CodeHash);
        var actual = Encoding.ASCII.GetBytes(HashSmsCode(phone, request.Code.Trim()));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            smsCode.Attempts++;
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException(InvalidSmsCode);
        }

        smsCode.UsedAt = now;
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Phone == phone, ct)
            ?? throw new UnauthorizedException(InvalidSmsCode);
        var user = await GetOrCreateCustomerUserAsync(customer);
        if (!user.IsActive)
            throw new UnauthorizedException("Учётная запись заблокирована");

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Покупатель {UserId} вошёл по SMS", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    private async Task<AppUser> GetOrCreateCustomerUserAsync(Customer customer)
    {
        if (customer.UserId is { } userId && await userManager.FindByIdAsync(userId.ToString()) is { } existing)
            return existing;

        var user = new AppUser
        {
            UserName = customer.Phone,
            PhoneNumber = customer.Phone,
            PhoneNumberConfirmed = true,
            FullName = customer.FullName
        };
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new ConflictException("Не удалось создать учётную запись покупателя: " +
                                        string.Join("; ", result.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(user, RoleNames.Customer);
        customer.UserId = user.Id;
        return user;
    }

    private static string HashSmsCode(string phone, string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{phone}:{code}")));

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
            throw UnknownAccountFailure(email, request.Password);

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException(LockedOut);

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Неудачный вход для пользователя {UserId}", user.Id);
            throw new UnauthorizedException(InvalidCredentials);
        }

        await userManager.ResetAccessFailedCountAsync(user);
        logger.LogInformation("Пользователь {UserId} вошёл в систему", user.Id);
        return await IssueTokensAsync(user, ct);
    }

    // Для несуществующей или отключённой почты ведём такой же счётчик ошибок, как Identity для настоящей,
    // и так же тратим время на проверку пароля: иначе по ответу или по задержке можно узнать, кто сотрудник.
    private UnauthorizedException UnknownAccountFailure(string email, string password)
    {
        userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyPasswordHash.Value, password);

        var lockout = identityOptions.Value.Lockout;
        var failures = cache.GetOrCreate("login-failures:" + email.ToUpperInvariant(), entry =>
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
        var now = timeProvider.GetUtcNow().UtcDateTime;
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

        if (!stored.IsActive(now) || !stored.User.IsActive)
            throw new UnauthorizedException(InvalidRefreshToken);

        return await IssueTokensAsync(stored.User, ct, stored);
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        var hash = JwtTokenService.HashToken(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || stored.RevokedAt is not null)
            return;

        stored.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Пользователь не найден");
        return ToResponse(user, await userManager.GetRolesAsync(user));
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken ct, RefreshToken? replacing = null)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiresAt) = tokens.CreateAccessToken(user, roles);
        var (refreshToken, refreshHash, refreshExpiresAt) = tokens.CreateRefreshToken();

        if (replacing is not null)
        {
            replacing.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
            replacing.ReplacedByTokenHash = refreshHash;
        }

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshHash,
            ExpiresAt = refreshExpiresAt
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt, ToResponse(user, roles));
    }

    private Task RevokeAllAsync(Guid userId, DateTime now, CancellationToken ct) =>
        db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);

    private static CurrentUserResponse ToResponse(AppUser user, IList<string> roles) =>
        new(user.Id, user.Email, user.PhoneNumber, user.FullName, roles.ToList());
}
