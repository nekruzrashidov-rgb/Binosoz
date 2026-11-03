namespace Sitora.Domain.Interfaces;

// Платёжный шлюз для онлайн-оплаты картой. Сейчас — MockPaymentGateway (имитация банка);
// потом его заменит настоящий шлюз (Alif, Корти Миллӣ) — одной строкой регистрации в DI.
public interface IPaymentGateway
{
    // "Mock", "Alif", "DC".
    string ProviderName { get; }

    Task<PaymentInitResult> InitiateAsync(PaymentInitRequest request);

    // Неизвестный шлюзу платёж — NotFoundException.
    Task<PaymentStatusResult> CheckStatusAsync(string gatewayPaymentId);

    Task<RefundResult> RefundAsync(string gatewayPaymentId, decimal amount);
}

// Currency — "TJS" или "USD"; ReturnUrl — куда банк вернёт покупателя; WebhookUrl — куда банк пришлёт результат.
public record PaymentInitRequest(
    Guid DealId,
    decimal Amount,
    string Currency,
    string Description,
    string ReturnUrl,
    string WebhookUrl);

// PaymentUrl — страница банка, куда отправить покупателя.
public record PaymentInitResult(
    bool Success,
    string? GatewayPaymentId,
    string? PaymentUrl,
    string? ErrorMessage);

public record PaymentStatusResult(
    string GatewayPaymentId,
    PaymentGatewayStatus Status,
    decimal Amount,
    string Currency,
    DateTime? PaidAt);

public record RefundResult(bool Success, string? ErrorMessage);

public enum PaymentGatewayStatus { Pending, Paid, Failed, Refunded }
