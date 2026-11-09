using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;

namespace Sitora.Domain.Interfaces;

// Разделы суперадмина: журнал платформы, рассылка уведомлений, настройки платформы.
public interface IPlatformToolsService
{
    Task<PagedResult<PlatformActivityItem>> ActivityAsync(PlatformActivityQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ActivityActionsAsync(CancellationToken ct = default);
    Task<PlatformNotificationResponse> SendNotificationAsync(PlatformNotificationRequest request, CancellationToken ct = default);
    Task<PlatformSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    Task<PlatformSettingsResponse> UpdateSettingsAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default);

    // Перед созданием организации: если задан лимит и он достигнут — BusinessRuleException.
    Task EnsureOrganizationLimitAsync(CancellationToken ct = default);
}
