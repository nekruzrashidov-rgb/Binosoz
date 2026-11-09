using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Суперадмин: журнал всей платформы, уведомления пользователям организаций, настройки платформы.
[ApiController]
[Route("api/v1/platform")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public class PlatformToolsController(IPlatformToolsService tools) : ControllerBase
{
    /// <summary>Журнал действий всех организаций (новые первыми): время, действие, организация, пользователь, IP.</summary>
    [HttpGet("activity")]
    [ProducesResponseType<PagedResult<PlatformActivityItem>>(StatusCodes.Status200OK)]
    public Task<PagedResult<PlatformActivityItem>> Activity([FromQuery] PlatformActivityQuery query, CancellationToken ct) =>
        tools.ActivityAsync(query, ct);

    /// <summary>Действия, которые есть в журнале, — для фильтра.</summary>
    [HttpGet("activity/actions")]
    public Task<IReadOnlyList<string>> ActivityActions(CancellationToken ct) => tools.ActivityActionsAsync(ct);

    /// <summary>
    /// Уведомление в колокольчик всем пользователям организации (organizationId) или всех организаций (без него).
    /// Тип: info, warning, error. Ответ — сколько пользователей его получили.
    /// </summary>
    [HttpPost("notifications")]
    [ProducesResponseType<PlatformNotificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<PlatformNotificationResponse> Notify(PlatformNotificationRequest request, CancellationToken ct) =>
        tools.SendNotificationAsync(request, ct);

    /// <summary>Настройки платформы: лимит организаций, демо-режим, версия (только чтение), контактная почта.</summary>
    [HttpGet("settings")]
    public Task<PlatformSettingsResponse> Settings(CancellationToken ct) => tools.GetSettingsAsync(ct);

    /// <summary>Изменить переданные настройки; clearMaxOrganizations = true снимает лимит организаций.</summary>
    [HttpPatch("settings")]
    [ProducesResponseType<PlatformSettingsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<PlatformSettingsResponse> UpdateSettings(UpdatePlatformSettingsRequest request, CancellationToken ct) =>
        tools.UpdateSettingsAsync(request, ct);
}
