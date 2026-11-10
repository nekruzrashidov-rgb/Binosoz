using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

// Личный кабинет покупателя. График, платежи, документы и акт сверки —
// по общим адресам /deals/{id}/..., там же проверяется, что сделка своя.
[ApiController]
[Route("api/v1/me")]
[Authorize(Roles = Roles.Customer)]
[Produces("application/json")]
public class CabinetController(IDealService deals) : ControllerBase
{
    [HttpGet("deals")]
    public Task<IReadOnlyList<DealResponse>> Deals(CancellationToken ct) => deals.GetMyDealsAsync(ct);
}

// Первый экран нового кабинета: есть ли сделки, имя, контакты застройщика и сводка по каждой квартире.
[ApiController]
[Route("api/v1/cabinet")]
[Authorize(Roles = Roles.Customer)]
[Produces("application/json")]
public class CabinetOverviewController(ICabinetService cabinet) : ControllerBase
{
    /// <summary>
    /// hasDeals = false — сделок ещё нет (кабинет показывает приветствие и контакты). Иначе deals[] — свои сделки, новые сверху:
    /// квартира, цена со скидкой, оплачено (за вычетом сторно), остаток, процент, менеджер, свой отзыв о доме (myReview).
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType<CabinetOverviewResponse>(StatusCodes.Status200OK)]
    public Task<CabinetOverviewResponse> Overview(CancellationToken ct) => cabinet.GetOverviewAsync(ct);
}
