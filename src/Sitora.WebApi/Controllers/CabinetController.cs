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
