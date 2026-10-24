using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

public interface ILeadService
{
    Task<LeadResponse> CreateAsync(CreateLeadRequest request, CancellationToken ct = default);
}
