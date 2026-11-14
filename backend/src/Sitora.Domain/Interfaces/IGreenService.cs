using Sitora.Domain.DTOs.Green;

namespace Sitora.Domain.Interfaces;

// «Зелёная» часть платформы: настройки застройщика, расчёт расходов квартиры, счётчики «Зелёного эффекта».
public interface IGreenService
{
    Task<GreenSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    Task<GreenSettingsResponse> SaveSettingsAsync(SaveGreenSettingsRequest request, CancellationToken ct = default);
    Task<UtilityEstimateResponse?> GetUtilityEstimateAsync(Guid apartmentId, CancellationToken ct = default);
    Task<GreenImpactResponse> GetPublicImpactAsync(string organizationSlug, CancellationToken ct = default);
    Task<GreenStatsResponse> GetStatsAsync(CancellationToken ct = default);
}
