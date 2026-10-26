using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

public interface ICrmService
{
    Task<CrmSummaryResponse> GetSummaryAsync(CancellationToken ct = default);
    Task<PipelineResponse> GetPipelineAsync(CancellationToken ct = default);
}
