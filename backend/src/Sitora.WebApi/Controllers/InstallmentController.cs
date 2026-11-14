using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Settings;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Условия рассрочки застройщика для сайта. Публично они видны в ответе дома (поле installment).
[ApiController]
[Route("api/v1/owner/installment-settings")]
[Produces("application/json")]
[Authorize(Roles = Roles.OrganizationLeaders)]
public class InstallmentController(IInstallmentService installment) : ControllerBase
{
    /// <summary>Минимальный первоначальный взнос (%), максимальный срок (месяцев) и признак «без переплаты».</summary>
    [HttpGet]
    public Task<InstallmentSettingsResponse> Get(CancellationToken ct) => installment.GetSettingsAsync(ct);

    /// <summary>Сохранить условия целиком; пустое поле — «не задано», тогда сайт это условие не пишет.</summary>
    [HttpPut]
    public Task<InstallmentSettingsResponse> Save(SaveInstallmentSettingsRequest request, CancellationToken ct) =>
        installment.SaveSettingsAsync(request, ct);
}
