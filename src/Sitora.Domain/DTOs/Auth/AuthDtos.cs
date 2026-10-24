namespace Sitora.Domain.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

public record LogoutRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    CurrentUserResponse User);

public record CurrentUserResponse(Guid Id, string? Email, string? Phone, string FullName, IReadOnlyList<string> Roles);
