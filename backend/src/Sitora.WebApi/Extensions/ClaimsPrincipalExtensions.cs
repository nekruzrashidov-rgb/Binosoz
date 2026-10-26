using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.Exceptions;

namespace Sitora.WebApi.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedException("Требуется вход");
}
