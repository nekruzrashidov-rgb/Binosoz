using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Пользователи всей платформы — только суперадмин.
[ApiController]
[Route("api/v1/platform/users")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public class PlatformUsersController(IPlatformUserService users) : ControllerBase
{
    /// <summary>
    /// Все пользователи: имя, почта, роль, организация, дата регистрации, последний вход, статус (Active / Blocked),
    /// сделки покупателя. Фильтры: role (SuperAdmin, Owner, ObjectAdmin, Manager, Accountant, Content, Customer),
    /// organizationId, search (имя, почта, телефон). Неизвестная роль — 422.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<PlatformUserResponse>> List(string? role, Guid? organizationId, string? search,
        int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        users.GetAsync(role, organizationId, search, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<PlatformUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<PlatformUserResponse> Get(Guid id, CancellationToken ct) => users.GetByIdAsync(id, ct);

    /// <summary>Заблокировать: вход закрыт (блокировка на 100 лет), все сессии отозваны. Себя и суперадмина — 422. Аудит Blocked.</summary>
    [HttpPost("{id:guid}/block")]
    [ProducesResponseType<PlatformUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<PlatformUserResponse> Block(Guid id, CancellationToken ct) => users.BlockAsync(id, ct);

    /// <summary>Разблокировать: вход снова открыт, счётчик неудачных попыток сброшен. Аудит Unblocked.</summary>
    [HttpPost("{id:guid}/unblock")]
    [ProducesResponseType<PlatformUserResponse>(StatusCodes.Status200OK)]
    public Task<PlatformUserResponse> Unblock(Guid id, CancellationToken ct) => users.UnblockAsync(id, ct);

    /// <summary>Новый пароль сотруднику (8 букв и цифр) — письмом; прежний не действует, сессии закрыты. Покупателю — 422.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken ct)
    {
        await users.ResetPasswordAsync(id, ct);
        return NoContent();
    }
}
