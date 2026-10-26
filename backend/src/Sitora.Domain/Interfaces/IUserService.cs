using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

// Сотрудники своей организации: владелец видит всех, админ объекта — сотрудников своего дома.
public interface IUserService
{
    Task<PagedResult<UserResponse>> GetAsync(int page, int pageSize, CancellationToken ct = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserResponse> SetStatusAsync(Guid id, SetUserStatusRequest request, CancellationToken ct = default);
    Task<UserResponse> SetBuildingsAsync(Guid id, SetUserBuildingsRequest request, CancellationToken ct = default);
}

public interface IInvitationService
{
    Task<InvitationResponse> CreateAsync(CreateInvitationRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<InvitationResponse>> GetPendingAsync(CancellationToken ct = default);
    Task RevokeAsync(Guid id, CancellationToken ct = default);
}

// Только для создателя платформы.
public interface IPlatformService
{
    Task<IReadOnlyList<OrganizationResponse>> GetOrganizationsAsync(CancellationToken ct = default);
    Task<OrganizationResponse> CreateOrganizationAsync(CreateOrganizationRequest request, CancellationToken ct = default);
    Task<AccessTokenResponse> ImpersonateAsync(Guid organizationId, CancellationToken ct = default);
    Task<AccessTokenResponse> StopImpersonationAsync(CancellationToken ct = default);
    Task<TotpSetupResponse> GetTotpSetupAsync(CancellationToken ct = default);
    Task EnableTotpAsync(TotpEnableRequest request, CancellationToken ct = default);
}
