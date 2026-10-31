using Sitora.Domain.DTOs.Reports;

namespace Sitora.Domain.Interfaces;

// Статистика CRM по домам, которые видит сотрудник (фильтры организации и домов в AppDbContext).
public interface IStatsService
{
    Task<DashboardStatsResponse> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MonthlySalesRow>> GetSalesAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default);
    Task<IReadOnlyList<BuildingStatsRow>> GetBuildingsAsync(CancellationToken ct = default);

    // Excel: сделки, график платежей, поступления за период и сводка по домам.
    Task<byte[]> ExportAsync(DateOnly? from, DateOnly? to, Guid? buildingId, CancellationToken ct = default);
}
