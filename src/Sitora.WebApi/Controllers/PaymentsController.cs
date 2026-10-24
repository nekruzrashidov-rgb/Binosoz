using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class PaymentsController(IPaymentService payments) : ControllerBase
{
    /// <summary>Составить (или пересоставить, пока нет платежей) график платежей.</summary>
    [HttpPost("deals/{dealId:guid}/plan")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<ScheduleResponse>(StatusCodes.Status200OK)]
    public Task<ScheduleResponse> CreatePlan(Guid dealId, CreatePlanRequest request, CancellationToken ct) =>
        payments.CreatePlanAsync(dealId, request, ct);

    [HttpGet("deals/{dealId:guid}/schedule")]
    [Authorize(Roles = Roles.DealViewers)]
    [ProducesResponseType<ScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ScheduleResponse> Schedule(Guid dealId, CancellationToken ct) =>
        payments.GetScheduleAsync(dealId, ct);

    [HttpPost("deals/{dealId:guid}/payments")]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PaymentResponse>> AddPayment(Guid dealId, CreatePaymentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await payments.AddPaymentAsync(dealId, request, ct));

    /// <summary>Сторно: обратная запись со ссылкой на исходный платёж. Платежи не удаляются.</summary>
    [HttpPost("payments/{paymentId:guid}/reverse")]
    [Authorize(Roles = Roles.Accounting)]
    [ProducesResponseType<PaymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PaymentResponse>> Reverse(Guid paymentId, ReversePaymentRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await payments.ReverseAsync(paymentId, request.Reason, ct));

    /// <summary>Акт сверки в PDF на дату (по умолчанию — сегодня по Душанбе).</summary>
    [HttpGet("deals/{dealId:guid}/statement.pdf")]
    [Authorize(Roles = Roles.DealViewers)]
    [Produces("application/pdf")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Statement(Guid dealId, DateOnly? date, CancellationToken ct)
    {
        var pdf = await payments.GetStatementPdfAsync(dealId, date, ct);
        return File(pdf, "application/pdf", $"akt-sverki-{date?.ToString("yyyy-MM-dd") ?? "today"}.pdf");
    }
}
