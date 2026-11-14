using Sitora.Domain.DTOs.Settings;

namespace Sitora.Domain.Interfaces;

public interface IInstallmentService
{
    Task<InstallmentSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    Task<InstallmentSettingsResponse> SaveSettingsAsync(SaveInstallmentSettingsRequest request, CancellationToken ct = default);
}
