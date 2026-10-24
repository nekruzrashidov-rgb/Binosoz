using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

public class AuthService(
    AppDbContext db,
    UserManager<AppUser> userManager,
    JwtTokenService tokens,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    private const string InvalidCredentials = "Неверная почта или пароль";
    private const string InvalidRefreshToken = "Сессия истекла, войдите заново";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive)
            throw new UnauthorizedException(InvalidCredentials);

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException("Слишком много неудачных попыток входа, попробуйте позже");

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
