using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = Roles.Admin)]
[Produces("application/json")]
public class UsersController(IUserService users) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserResponse>> List(int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        users.GetAsync(page, pageSize, ct);

    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(CreateUserRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await users.CreateAsync(request, ct));

    /// <summary>Заблокировать или разблокировать пользователя; при блокировке отзываются все его сессии.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public Task<UserResponse> SetStatus(Guid id, SetUserStatusRequest request, CancellationToken ct) =>
        users.SetStatusAsync(id, request, ct);
}
