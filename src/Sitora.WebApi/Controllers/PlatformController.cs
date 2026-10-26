using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Раздел создателя платформы: организации (застройщики), вход «как владелец», второй фактор.
[ApiController]
[Route("api/v1/platform")]
[Produces("application/json")]
public class PlatformController(IPlatformService platform) : ControllerBase
{
    [HttpGet("organizations")]
    [Authorize(Roles = Roles.PlatformOwner)]
    public Task<IReadOnlyList<OrganizationResponse>> Organizations(CancellationToken ct) =>
        platform.GetOrganizationsAsync(ct);

    /// <summary>Новая организация и её владелец (входит по коду из почты).</summary>
    [HttpPost("organizations")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(CreateOrganizationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await platform.CreateOrganizationAsync(request, ct));

    /// <summary>
    /// Войти как владелец организации: access-токен без refresh с ролью Owner и пометкой imp.
    /// Каждое действие в этом режиме пишется в аудит с настоящим автором.
    /// </summary>
    [HttpPost("impersonate/{organizationId:guid}")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public Task<AccessTokenResponse> Impersonate(Guid organizationId, CancellationToken ct) =>
        platform.ImpersonateAsync(organizationId, ct);

    /// <summary>Выйти из режима «войти как владелец»: обычный access-токен создателя платформы.</summary>
    [HttpPost("impersonate/stop")]
    [Authorize]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    public Task<AccessTokenResponse> StopImpersonation(CancellationToken ct) =>
        platform.StopImpersonationAsync(ct);

    /// <summary>Ключ для приложения-аутентификатора (второй фактор вместо пароля).</summary>
    [HttpGet("totp")]
    [Authorize(Roles = Roles.PlatformOwner)]
    public Task<TotpSetupResponse> TotpSetup(CancellationToken ct) =>
        platform.GetTotpSetupAsync(ct);

    [HttpPost("totp/enable")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> EnableTotp(TotpEnableRequest request, CancellationToken ct)
    {
        await platform.EnableTotpAsync(request, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/v1/invitations")]
[Authorize(Roles = Roles.Leaders)]
[Produces("application/json")]
public class InvitationsController(IInvitationService invitations) : ControllerBase
{
    /// <summary>
    /// Пригласить сотрудника по почте. Владелец: ObjectAdmin (ровно один дом), Manager, Accountant, Content
    /// (дома необязательны — пусто значит все). Админ объекта: Manager, Accountant, Content только в свой дом.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<InvitationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InvitationResponse>> Create(CreateInvitationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await invitations.CreateAsync(request, ct));

    [HttpGet]
    public Task<IReadOnlyList<InvitationResponse>> Pending(CancellationToken ct) =>
        invitations.GetPendingAsync(ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await invitations.RevokeAsync(id, ct);
        return NoContent();
    }
}
