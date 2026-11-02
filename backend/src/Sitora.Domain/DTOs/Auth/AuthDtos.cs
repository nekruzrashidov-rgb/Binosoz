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

// Role — главная роль для выбора интерфейса (SuperAdmin > Owner > ObjectAdmin > Manager > Accountant > Content > Customer).
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

// Регистрация покупателя на сайте застройщика (без пароля: дальше вход по коду из почты).
// Organization — короткое имя застройщика (slug); без него — единственный застройщик платформы.
public record RegisterCustomerRequest(string Email, string FirstName, string LastName, string Phone, string? Organization = null);

// Пароль принят, но у учётной записи включена 2FA: токены выдаст /auth/2fa/verify с кодом из приложения.
public record TwoFactorRequiredResponse(bool RequiresTwoFactor, string TempToken, DateTime ExpiresAt);

// Второй шаг входа: временный токен из /auth/login и 6 цифр из приложения-аутентификатора.
public record TwoFactorVerifyRequest(string TempToken, string Code);

// Результат входа по паролю: либо токены, либо запрос кода 2FA.
public record LoginResult(AuthResponse? Auth, TwoFactorRequiredResponse? TwoFactor);

// Токен без refresh-токена: режим «войти как владелец» и выход из него.
public record AccessTokenResponse(string AccessToken, DateTime AccessTokenExpiresAt, CurrentUserResponse User);

// «Забыли пароль?» сотрудника: письмо со ссылкой (24 часа). Ответ одинаковый для любой почты.
public record PasswordResetRequest(string Email);

// Новый пароль по токену из ссылки: от 8 символов, с цифрой, строчной и заглавной буквами.
public record PasswordResetConfirmRequest(string Token, string NewPassword);

public record MessageResponse(string Message);

// Настройка 2FA: qrCodeUri — для QR-кода (otpauth://totp/Binosoz:{email}?secret=…&issuer=Binosoz), manualKey — ввести руками.
public record TwoFactorSetupResponse(string QrCodeUri, string ManualKey);

// Код из приложения-аутентификатора: включить или отключить 2FA.
public record TwoFactorCodeRequest(string Code);
