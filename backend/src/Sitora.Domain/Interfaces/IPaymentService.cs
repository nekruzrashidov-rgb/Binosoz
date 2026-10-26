using Sitora.Domain.DTOs.Payments;

namespace Sitora.Domain.Interfaces;

public interface IPaymentService
{
    Task<ScheduleResponse> CreatePlanAsync(Guid dealId, CreatePlanRequest request, CancellationToken ct = default);
    Task<ScheduleResponse> GetScheduleAsync(Guid dealId, CancellationToken ct = default);
    Task<byte[]> GetSchedulePdfAsync(Guid dealId, CancellationToken ct = default);
    Task<PaymentResponse> AddPaymentAsync(Guid dealId, CreatePaymentRequest request, CancellationToken ct = default);
    Task<PaymentResponse> ReverseAsync(Guid paymentId, string reason, CancellationToken ct = default);

    Task<StatementData> GetStatementDataAsync(Guid dealId, DateOnly? asOf, CancellationToken ct = default);
    Task<byte[]> GetStatementPdfAsync(Guid dealId, DateOnly? asOf, CancellationToken ct = default);
}
