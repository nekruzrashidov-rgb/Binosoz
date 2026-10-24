using Sitora.Domain.DTOs.Auth;

namespace Sitora.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
