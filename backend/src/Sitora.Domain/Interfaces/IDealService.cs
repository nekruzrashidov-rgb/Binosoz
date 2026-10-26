using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

public interface IDealService
{
    Task<PagedResult<DealResponse>> GetAsync(DealStage? stage, Guid? customerId, int page, int pageSize, CancellationToken ct = default);
    Task<DealResponse> GetByIdAsync(Guid id, CancellationToken ct = default);

    // Сделки текущего покупателя для личного кабинета.
    Task<IReadOnlyList<DealResponse>> GetMyDealsAsync(CancellationToken ct = default);
    Task<DealResponse> CreateAsync(CreateDealRequest request, CancellationToken ct = default);
    Task<DealResponse> ChangeStageAsync(Guid id, ChangeDealStageRequest request, CancellationToken ct = default);
}
