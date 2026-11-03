using Sitora.Domain.Enums;
using Sitora.Domain.Interfaces;

namespace Sitora.Domain.DTOs.Payments;

// Онлайн-оплата картой. Amount — сколько заплатить (в валюте договора); без него — ближайший неоплаченный платёж графика.
// ReturnUrl — куда вернуть после оплаты: только путь на этом сайте ("/cabinet?success=1"), без него — кабинет.
public record InitiatePaymentRequest(decimal? Amount = null, string? ReturnUrl = null);

public record InitiatePaymentResponse(string PaymentUrl, string GatewayPaymentId, decimal Amount, Currency Currency, string Provider);

// PaymentId — платёж в графике, созданный, когда шлюз подтвердил оплату.
public record OnlinePaymentStatusResponse(
    string GatewayPaymentId,
    PaymentGatewayStatus Status,
    decimal Amount,
    Currency Currency,
    DateTime? PaidAt,
    Guid? PaymentId);

public record SimulatePaymentRequest(string GatewayPaymentId, bool Success);
