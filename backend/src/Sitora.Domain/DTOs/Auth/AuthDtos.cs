namespace Sitora.Domain.DTOs.Auth;

public record LoginRequest(string Email, string Password);

public record RefreshRequest(string RefreshToken);

// Активный вход: устройство по User-Agent («Chrome · Windows»), когда вошли, когда последний раз обновляли токен,
// до какого числа вход живёт без визитов; Current — это устройство (вход текущего access-токена).
public record SessionResponse(Guid Id, string? Device, DateTime StartedAt, DateTime LastActiveAt, DateTime ExpiresAt, bool Current);

public record SessionsRevokedResponse(int Revoked);

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

// Письмо с кодом подтверждения отправлено: код живёт ExpiresInSeconds, новый можно запросить через ResendAfterSeconds.
public record EmailSendResponse(string Message, int ExpiresInSeconds, int ResendAfterSeconds = 60);

// GET /auth/methods: какие способы входа сейчас работают. Pin — вход покупателя по своему 6-значному коду (всегда).
// Telegram — вход по телефону кодом в Telegram (Telegram Gateway настроен).
public record AuthMethodsResponse(bool Email, bool Sms, bool Demo = false, bool Pin = true, bool Telegram = false);

// Регистрация покупателя: имя, почта, телефон (для карточки клиента) и свой 6-значный код входа.
// Organization — короткое имя застройщика (slug); без него — единственный застройщик платформы.
public record CustomerRegisterRequest(string Name, string Email, string? Phone, string Pin, string? Organization = null);

// Подтверждение почты кодом из письма (регистрация) — выдаёт токены.
public record CustomerConfirmRequest(string Email, string Code);

// Только почта: повторить код подтверждения, запросить сброс кода входа.
public record CustomerEmailRequest(string Email);

// Вход покупателя: почта и свой 6-значный код.
public record CustomerLoginRequest(string Email, string Pin);

// Новый код входа по коду из письма «Забыли код?».
public record CustomerPinResetRequest(string Email, string Code, string NewPin);

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
