using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Brand;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Enums;
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

    /// <summary>Короткое имя (поддомен) и свой домен организации; пустой домен — без своего домена.</summary>
    [HttpPut("organizations/{organizationId:guid}/domain")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<BrandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BrandResponse> SetDomain(Guid organizationId, UpdateOrganizationDomainRequest request, [FromServices] IBrandService brands,
        CancellationToken ct) =>
        brands.SetDomainAsync(organizationId, request, ct);

    /// <summary>Добавить владельца организации: почта, имя и пароль (необязательно — вход по коду из почты).</summary>
    [HttpPost("organizations/{organizationId:guid}/owners")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> AddOwner(Guid organizationId, AddOwnerRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await platform.AddOwnerAsync(organizationId, request, ct));

    /// <summary>Заявки застройщиков с публичной страницы; status — Pending, Approved или Rejected, без него — все.</summary>
    [HttpGet("applications")]
    [Authorize(Roles = Roles.PlatformOwner)]
    public Task<PagedResult<OrgApplicationResponse>> Applications([FromServices] IOrgApplicationService applications,
        OrgApplicationStatus? status, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        applications.GetAsync(status, PageRequest.Normalize(page, pageSize), ct);

    /// <summary>Одобрить заявку: создаётся организация с этим коротким именем, владельцу уходит письмо со входом.</summary>
    [HttpPost("applications/{id:guid}/approve")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<ApproveApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ApproveApplicationResponse> ApproveApplication(Guid id, ApproveApplicationRequest request,
        [FromServices] IOrgApplicationService applications, CancellationToken ct) =>
        applications.ApproveAsync(id, request, ct);

    /// <summary>Отклонить заявку с причиной; заявителю уходит письмо.</summary>
    [HttpPost("applications/{id:guid}/reject")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectApplication(Guid id, RejectApplicationRequest request,
        [FromServices] IOrgApplicationService applications, CancellationToken ct)
    {
        await applications.RejectAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>
    /// Войти «как владелец» организации (id организации) или под конкретным пользователем (id пользователя):
    /// access-токен без refresh с пометкой imp. Каждое действие в этом режиме пишется в аудит.
    /// </summary>
    [HttpPost("impersonate/{id:guid}")]
    [Authorize(Roles = Roles.PlatformOwner)]
    [ProducesResponseType<AccessTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AccessTokenResponse> Impersonate(Guid id, CancellationToken ct) =>
        platform.ImpersonateAsync(id, ct);

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
