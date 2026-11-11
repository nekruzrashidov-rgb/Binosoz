using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Онлайн-оплата картой через платёжный шлюз. Сейчас шлюз — имитация банка (Mock), страница «банка» — /payment/mock-card.
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class PaymentGatewayController(IOnlinePaymentService online, IHostEnvironment environment) : ControllerBase
{
    /// <summary>
    /// Начать оплату картой: покупатель — по своей сделке (чужая — 404), сотрудники — по сделкам своих домов.
    /// amount — сумма в валюте договора (без неё — ближайший неоплаченный платёж графика, не больше остатка);
    /// returnUrl — путь на этом сайте, куда вернуть после оплаты (по умолчанию /cabinet?success=1).
    /// Ответ — paymentUrl (страница банка) и gatewayPaymentId. Платёж в графике появится после подтверждения банком.
    /// </summary>
    [HttpPost("deals/{dealId:guid}/payment/initiate")]
    [Authorize(Roles = Roles.DealViewers)]
    [ProducesResponseType<InitiatePaymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<InitiatePaymentResponse> Initiate(Guid dealId, [FromBody] InitiatePaymentRequest? request, CancellationToken ct) =>
        online.InitiateAsync(dealId, request ?? new InitiatePaymentRequest(), ct);

    /// <summary>
    /// Результат онлайн-оплаты: Pending, Paid, Failed или Refunded. Когда банк подтвердил оплату, создаётся платёж по графику
    /// (метод Card, журнал, уведомления сотрудникам и покупателю в Telegram) — один раз, сколько бы раз ни спрашивали.
    /// </summary>
    [HttpGet("deals/{dealId:guid}/payment/{gatewayPaymentId}/status")]
    [Authorize(Roles = Roles.DealViewers)]
    [ProducesResponseType<OnlinePaymentStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<OnlinePaymentStatusResponse> Status(Guid dealId, string gatewayPaymentId, CancellationToken ct) =>
        online.GetStatusAsync(dealId, gatewayPaymentId, ct);

    /// <summary>
    /// Только в Development и с тестовым шлюзом: «банк» принял (success = true) или отклонил платёж. Вне Development — 404.
    /// Вызывает страница /payment/mock-card вместо настоящего банка.
    /// </summary>
    [HttpPost("payment/mock/simulate")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<OnlinePaymentStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Simulate(SimulatePaymentRequest request, CancellationToken ct) =>
        environment.IsDevelopment() ? Ok(await online.SimulateAsync(request, ct)) : NotFound();

    // Банки, уведомления которых будут приниматься. Остальные адреса — 404: строка из URL в ответ не попадает.
    private static readonly Dictionary<string, string> WebhookProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alif"] = "Alif",
        ["korti-milli"] = "Корти Миллӣ"
    };

    /// <summary>Заготовка для уведомлений банка: alif, korti-milli — пока 501; другие провайдеры — 404.</summary>
    [HttpPost("payment/webhook/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status501NotImplemented)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult Webhook(string provider) => WebhookProviders.TryGetValue(provider, out var name)
        ? Problem(statusCode: StatusCodes.Status501NotImplemented, title: "Не реализовано",
            detail: $"Уведомления платёжного шлюза «{name}» ещё не подключены")
        : throw new Sitora.Domain.Exceptions.NotFoundException("Платёжный шлюз не найден");
}
