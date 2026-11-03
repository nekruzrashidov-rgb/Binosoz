using Sitora.Domain.DTOs.Platform;

namespace Sitora.Domain.Interfaces;

// Сводка по всей платформе — только для суперадмина.
public interface IPlatformDashboardService
{
    Task<PlatformDashboardResponse> GetAsync(CancellationToken ct = default);
}
