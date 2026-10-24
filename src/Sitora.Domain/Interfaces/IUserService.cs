using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserResponse>> GetAsync(int page, int pageSize, CancellationToken ct = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
}
