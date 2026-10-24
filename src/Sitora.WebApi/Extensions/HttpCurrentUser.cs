using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Extensions;

// Пользователь вызова хаба SignalR. Хаб кладёт сюда Context.User,
// потому что HttpContext соединения в вызовах хаба надёжно не доступен.
public class HubCaller
{
    public ClaimsPrincipal? Principal { get; set; }
}

public class HttpCurrentUser(IHttpContextAccessor accessor, HubCaller hubCaller) : ICurrentUser
{
    private ClaimsPrincipal? Principal => hubCaller.Principal ?? accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value, out var id) ? id : null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}
