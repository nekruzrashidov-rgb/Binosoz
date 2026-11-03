using Sitora.Domain.DTOs.Payments;

namespace Sitora.Domain.Interfaces;

// Онлайн-оплата картой по сделке через платёжный шлюз (IPaymentGateway).
public interface IOnlinePaymentService
{
    Task<InitiatePaymentResponse> InitiateAsync(Guid dealId, InitiatePaymentRequest request, CancellationToken ct = default);
    Task<OnlinePaymentStatusResponse> GetStatusAsync(Guid dealId, string gatewayPaymentId, CancellationToken ct = default);

    // Только с тестовым шлюзом (Mock): «банк» принял или отклонил платёж.
    Task<OnlinePaymentStatusResponse> SimulateAsync(SimulatePaymentRequest request, CancellationToken ct = default);
}
