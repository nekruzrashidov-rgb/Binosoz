using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Sitora.Domain.Common;
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

    public Guid? UserId => ReadGuid(JwtRegisteredClaimNames.Sub);

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public Guid? OrganizationId => ReadGuid(AccessClaims.Organization);

    public IReadOnlyList<Guid>? BuildingIds
    {
        get
        {
            var ids = Principal?.FindAll(AccessClaims.Building)
                .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            return ids is { Count: > 0 } ? ids : null;
        }
    }

    public Guid? ImpersonatorId => ReadGuid(AccessClaims.ImpersonatedBy);

    private Guid? ReadGuid(string claim) =>
        Guid.TryParse(Principal?.FindFirst(claim)?.Value, out var id) ? id : null;
}
