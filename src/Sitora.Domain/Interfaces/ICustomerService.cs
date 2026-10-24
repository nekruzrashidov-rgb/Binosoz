using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

public interface ICustomerService
{
    Task<PagedResult<CustomerListItem>> GetAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<CustomerResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);
}
