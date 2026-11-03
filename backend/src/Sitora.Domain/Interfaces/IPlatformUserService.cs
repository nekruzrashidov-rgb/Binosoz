using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Platform;

namespace Sitora.Domain.Interfaces;

// Все пользователи платформы — для суперадмина: поиск, блокировка, новый пароль.
public interface IPlatformUserService
{
    Task<PagedResult<PlatformUserResponse>> GetAsync(string? role, Guid? organizationId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<PlatformUserResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PlatformUserResponse> BlockAsync(Guid id, CancellationToken ct = default);
    Task<PlatformUserResponse> UnblockAsync(Guid id, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, CancellationToken ct = default);
}
