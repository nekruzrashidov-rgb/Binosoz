using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

public interface ILeadService
{
    Task<LeadResponse> CreateAsync(CreateLeadRequest request, CancellationToken ct = default);
    Task<PagedResult<LeadResponse>> GetAsync(LeadStatus? status, int page, int pageSize, CancellationToken ct = default);
    Task<LeadResponse> UpdateAsync(Guid id, UpdateLeadRequest request, CancellationToken ct = default);
}
