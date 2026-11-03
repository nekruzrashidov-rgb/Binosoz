using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Brand;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Раздел суперадмина: организации (застройщики) и вход «как владелец». 2FA — /auth/2fa/*.
[ApiController]
[Route("api/v1/platform")]
[Produces("application/json")]
public class PlatformController(IPlatformService platform) : ControllerBase
{
    /// <summary>
    /// Дашборд платформы: организации (всего, активных, новых за месяц), дома, сделки (без отменённых), сумма платежей,
    /// покупатели, сотрудники, доход платформы за месяц (оплаченные тарифы), топ-5 организаций по сделкам и сделки
    /// по месяцам за последние 12 месяцев. Суммы — в сомони.
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<PlatformDashboardResponse>(StatusCodes.Status200OK)]
    public Task<PlatformDashboardResponse> Dashboard([FromServices] IPlatformDashboardService dashboard, CancellationToken ct) =>
        dashboard.GetAsync(ct);

    /// <summary>
    /// Все организации: название, slug, статус, дома, сотрудники, сделки, покупатели, дата регистрации,
    /// последняя активность (по журналу), действующий тариф и владельцы (имя, почта, телефон).
    /// </summary>
    [HttpGet("organizations")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public Task<IReadOnlyList<OrganizationResponse>> Organizations(CancellationToken ct) =>
        platform.GetOrganizationsAsync(ct);

    /// <summary>Карточка организации: данные списка, дома с числом квартир, сумма платежей (TJS), история тарифов.</summary>
    [HttpGet("organizations/{organizationId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<OrganizationDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OrganizationDetailsResponse> Organization(Guid organizationId, CancellationToken ct) =>
        platform.GetOrganizationAsync(organizationId, ct);

    /// <summary>Изменить название и (необязательно) короткое имя организации. Занятое короткое имя — 409. Аудит Organization / Updated.</summary>
    [HttpPut("organizations/{organizationId:guid}")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<OrganizationDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<OrganizationDetailsResponse> UpdateOrganization(Guid organizationId, UpdateOrganizationRequest request, CancellationToken ct) =>
        platform.UpdateOrganizationAsync(organizationId, request, ct);

    /// <summary>Приостановить организацию (данные не удаляются): вход сотрудникам и сайт закрыты, сессии отозваны. Аудит Suspended.</summary>
    [HttpDelete("organizations/{organizationId:guid}/suspend")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid organizationId, CancellationToken ct)
    {
        await platform.SetOrganizationActiveAsync(organizationId, false, ct);
        return NoContent();
    }

    /// <summary>Снова открыть приостановленную организацию. Аудит Activated.</summary>
    [HttpPost("organizations/{organizationId:guid}/activate")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Activate(Guid organizationId, CancellationToken ct)
    {
        await platform.SetOrganizationActiveAsync(organizationId, true, ct);
        return NoContent();
    }

    /// <summary>Пригласить владельца: учётная запись Owner создаётся сразу, пароль приходит письмом. Лимит тарифа — 402.</summary>
    [HttpPost("organizations/{organizationId:guid}/invite-owner")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> InviteOwner(Guid organizationId, InviteOwnerRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await platform.InviteOwnerAsync(organizationId, request, ct));

    /// <summary>Новая организация и её владелец (логин и пароль — письмом).</summary>
    [HttpPost("organizations")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<OrganizationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrganizationResponse>> CreateOrganization(CreateOrganizationRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await platform.CreateOrganizationAsync(request, ct));

    /// <summary>Короткое имя (поддомен) и свой домен организации; пустой домен — без своего домена.</summary>
    /// <summary>Приостановить или снова открыть организацию. При приостановке вход её сотрудникам закрыт, их сессии отзываются.</summary>
    [HttpPatch("organizations/{organizationId:guid}/status")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetStatus(Guid organizationId, OrganizationStatusRequest request, CancellationToken ct)
    {
        await platform.SetOrganizationActiveAsync(organizationId, request.IsActive, ct);
        return NoContent();
    }

    [HttpPut("organizations/{organizationId:guid}/domain")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<BrandResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<BrandResponse> SetDomain(Guid organizationId, UpdateOrganizationDomainRequest request, [FromServices] IBrandService brands,
        CancellationToken ct) =>
        brands.SetDomainAsync(organizationId, request, ct);

    /// <summary>Добавить владельца организации: почта, имя и пароль (необязательно — вход по коду из почты).</summary>
    [HttpPost("organizations/{organizationId:guid}/owners")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> AddOwner(Guid organizationId, AddOwnerRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await platform.AddOwnerAsync(organizationId, request, ct));

    /// <summary>Заявки застройщиков с публичной страницы; status — Pending, Approved или Rejected, без него — все.</summary>
    [HttpGet("applications")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public Task<PagedResult<OrgApplicationResponse>> Applications([FromServices] IOrgApplicationService applications,
        OrgApplicationStatus? status, int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        applications.GetAsync(status, PageRequest.Normalize(page, pageSize), ct);

    /// <summary>Одобрить заявку: создаётся организация с этим коротким именем, владельцу уходит письмо со входом.</summary>
    [HttpPost("applications/{id:guid}/approve")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType<ApproveApplicationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ApproveApplicationResponse> ApproveApplication(Guid id, ApproveApplicationRequest request,
        [FromServices] IOrgApplicationService applications, CancellationToken ct) =>
        applications.ApproveAsync(id, request, ct);

    /// <summary>Отклонить заявку с причиной; заявителю уходит письмо.</summary>
    [HttpPost("applications/{id:guid}/reject")]
    [Authorize(Roles = Roles.SuperAdmin)]
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
    [Authorize(Roles = Roles.SuperAdmin)]
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
