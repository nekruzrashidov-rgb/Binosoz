using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Middleware;

// Любой запрос вошедшего сотрудника отмечает, что он «в сети»: покупатель видит это в чате по сделке.
public class PresenceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IPresenceTracker presence)
    {
        if (context.User.Identity?.IsAuthenticated == true && !context.User.IsInRole(RoleNames.Customer)
            && Guid.TryParse(context.User.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId))
            await presence.TouchAsync(userId, context.RequestAborted);
        await next(context);
    }
}
