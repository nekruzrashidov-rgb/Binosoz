using Sitora.Domain.DTOs.Reports;

namespace Sitora.Domain.Interfaces;

public interface IReportService
{
    // Без дат — текущий месяц по Душанбе.
    Task<PaymentsReportResponse> GetPaymentsAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<DebtsReportResponse> GetDebtsAsync(DateOnly? asOf, CancellationToken ct = default);

    byte[] ToExcel(PaymentsReportResponse report);
    byte[] ToExcel(DebtsReportResponse report);
}
