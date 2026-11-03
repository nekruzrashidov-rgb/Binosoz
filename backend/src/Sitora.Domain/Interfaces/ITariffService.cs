using Sitora.Domain.DTOs.Platform;

namespace Sitora.Domain.Interfaces;

// Тарифы платформы (суперадмин) и лимиты организации по её действующему тарифу.
public interface ITariffService
{
    Task<IReadOnlyList<TariffPlanResponse>> GetAsync(CancellationToken ct = default);
    Task<TariffPlanResponse> CreateAsync(SaveTariffRequest request, CancellationToken ct = default);
    Task<TariffPlanResponse> UpdateAsync(Guid id, SaveTariffRequest request, CancellationToken ct = default);
    Task<OrganizationTariffResponse> AssignAsync(Guid organizationId, AssignTariffRequest request, CancellationToken ct = default);

    // Действующие тарифы организаций (для списков); у организации без тарифа записи нет.
    Task<IReadOnlyDictionary<Guid, OrganizationTariffResponse>> CurrentAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default);

    // 402, если по тарифу больше нельзя: у организации без тарифа лимитов нет.
    Task EnsureCanAddBuildingAsync(Guid organizationId, CancellationToken ct = default);
    Task EnsureCanAddUserAsync(Guid organizationId, CancellationToken ct = default);
}
