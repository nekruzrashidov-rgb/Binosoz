using Sitora.Domain.DTOs.Reports;

namespace Sitora.Domain.Interfaces;

// Еженедельные отчёты владельцу по дому. Чужой дом или отчёт чужого дома — 404.
public interface IWeeklyReportService
{
    Task<IReadOnlyList<WeeklyReportResponse>> ListAsync(Guid buildingId, CancellationToken ct = default);
    Task<byte[]> GetExcelAsync(Guid buildingId, Guid reportId, CancellationToken ct = default);
    Task<byte[]> GetPdfAsync(Guid buildingId, Guid reportId, CancellationToken ct = default);

    // Отчёт за последнюю завершённую неделю (пн–вс по Душанбе); уже готовый за ту же неделю перезаписывается.
    Task<WeeklyReportResponse> GenerateAsync(Guid buildingId, CancellationToken ct = default);

    // Для фоновой задачи: дома организаций с активным владельцем, у которых отчёта за прошлую неделю ещё нет.
    Task<int> GenerateForAllAsync(CancellationToken ct = default);
}
