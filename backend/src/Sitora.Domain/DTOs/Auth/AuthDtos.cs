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

// Role — главная роль для выбора интерфейса (PlatformOwner > Owner > ObjectAdmin > Manager > Accountant > Content > Customer).
// BuildingIds пустой — доступны все дома организации. Impersonation — токен выдан в режиме «войти как владелец».
public record CurrentUserResponse(
    Guid Id,
    string? Email,
    string? Phone,
    string FullName,
    IReadOnlyList<string> Roles,
    string? Role = null,
    Guid? OrganizationId = null,
    string? OrganizationName = null,
    IReadOnlyList<Guid>? BuildingIds = null,
    ImpersonationInfo? Impersonation = null);

// OrganizationId — организация, в которую вошли «как владелец»; при входе под конкретным пользователем — его организация (у покупателя null).
public record ImpersonationInfo(Guid ImpersonatedBy, Guid? OrganizationId);

// Вход по коду из почты.
public record EmailSendRequest(string Email);

public record EmailSendResponse(string Message, int ExpiresInSeconds);

public record EmailVerifyRequest(string Email, string Code);

// Регистрация покупателя на сайте застройщика: Organization — его короткое имя (slug); без него — единственный застройщик платформы.
public record RegisterCustomerRequest(string Email, string FullName, string Phone, string? Organization = null);

// Код из почты принят, но учётной записи нужен второй фактор (создатель платформы).
public record SecondFactorChallenge(string ChallengeToken, DateTime ExpiresAt, IReadOnlyList<string> Methods, string Message);

// Пароль или код из приложения-аутентификатора (TOTP) — что-то одно.
public record SecondFactorRequest(string ChallengeToken, string? Password, string? TotpCode);

// Результат проверки кода: либо токены, либо запрос второго фактора.
public record EmailLoginResult(AuthResponse? Auth, SecondFactorChallenge? Challenge);

// Токен без refresh-токена: режим «войти как владелец» и выход из него.
public record AccessTokenResponse(string AccessToken, DateTime AccessTokenExpiresAt, CurrentUserResponse User);

public record TotpSetupResponse(string SharedKey, string AuthenticatorUri);

public record TotpEnableRequest(string Code);
