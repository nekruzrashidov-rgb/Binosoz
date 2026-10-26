using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Sitora.Domain.Common;
using Sitora.Domain.Entities;

namespace Sitora.Infrastructure.Security;

public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
{
    public const string RoleClaimType = AccessClaims.Role;

    private readonly JwtOptions _options = options.Value;

    // Организация, дома и режим «войти как владелец» попадают в claims: по ним AppDbContext
    // ограничивает данные. Изменение домов сотрудника вступает в силу со следующим токеном (до 15 минут).
    public (string Token, DateTime ExpiresAt) CreateAccessToken(AppUser user, IEnumerable<string> roles, TokenScope scope)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Name, user.FullName)
        };
        if (user.Email is not null)
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        claims.AddRange(roles.Select(r => new Claim(RoleClaimType, r)));
        if (scope.OrganizationId is { } organizationId)
            claims.Add(new Claim(AccessClaims.Organization, organizationId.ToString()));
        claims.AddRange(scope.BuildingIds.Select(id => new Claim(AccessClaims.Building, id.ToString())));
        if (scope.ImpersonatedBy is { } impersonator)
            claims.Add(new Claim(AccessClaims.ImpersonatedBy, impersonator.ToString()));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(GetSigningKey(_options.Key), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    public (string Token, string Hash, DateTime ExpiresAt) CreateRefreshToken()
    {
        var token = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(_options.RefreshTokenDays);
        return (token, HashToken(token), expiresAt);
    }

    public static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static SymmetricSecurityKey GetSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
