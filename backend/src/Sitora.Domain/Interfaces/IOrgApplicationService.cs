using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Enums;

namespace Sitora.Domain.Interfaces;

// Заявки застройщиков: подача без входа, рассмотрение создателем платформы.
public interface IOrgApplicationService
{
    Task<SubmitApplicationResponse> SubmitAsync(SubmitApplicationRequest request, CancellationToken ct = default);
    Task<PagedResult<OrgApplicationResponse>> GetAsync(OrgApplicationStatus? status, PageRequest page, CancellationToken ct = default);
    Task<ApproveApplicationResponse> ApproveAsync(Guid id, ApproveApplicationRequest request, CancellationToken ct = default);
    Task RejectAsync(Guid id, RejectApplicationRequest request, CancellationToken ct = default);
}
