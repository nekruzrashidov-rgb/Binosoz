using Sitora.Domain.DTOs.Auth;

namespace Sitora.Domain.Interfaces;

public interface IAuthService
{
    // Сотрудники — по паролю (с 2FA, если она включена); покупателю — 400, он входит по коду.
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request, CancellationToken ct = default);

    // Восстановление пароля сотрудника: письмо со ссылкой, затем новый пароль по токену.
    Task<MessageResponse> RequestPasswordResetAsync(PasswordResetRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(PasswordResetConfirmRequest request, CancellationToken ct = default);

    // 2FA (TOTP) сотрудника: ключ для приложения, включить и отключить — кодом из приложения.
    Task<TwoFactorSetupResponse> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default);
    Task EnableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken ct = default);
    Task DisableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);

    // «Устройства»: активные входы пользователя (одна строка на сессию refresh-токенов), текущий — Current.
    Task<IReadOnlyList<SessionResponse>> GetSessionsAsync(Guid userId, CancellationToken ct = default);
    // Закрыть один вход (чужой или свой); не найден — 404.
    Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    // «Выйти на всех устройствах»: отзывает все refresh-токены пользователя, включая этот.
    Task<int> RevokeAllSessionsAsync(Guid userId, CancellationToken ct = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);

    // Покупатель: свой 6-значный код входа (PIN, хранится как пароль Identity). Регистрация и сброс подтверждаются
    // кодом из почты; ответы на запросы кода одинаковые, есть учётная запись или нет.
    Task<EmailSendResponse> RegisterCustomerAsync(CustomerRegisterRequest request, string? requestIp, CancellationToken ct = default);
    Task<EmailSendResponse> ResendConfirmationAsync(CustomerEmailRequest request, string? requestIp, CancellationToken ct = default);
    Task<AuthResponse> ConfirmCustomerAsync(CustomerConfirmRequest request, CancellationToken ct = default);
    Task<AuthResponse> CustomerLoginAsync(CustomerLoginRequest request, CancellationToken ct = default);
    Task<EmailSendResponse> RequestPinResetAsync(CustomerEmailRequest request, string? requestIp, CancellationToken ct = default);
    Task<AuthResponse> ResetPinAsync(CustomerPinResetRequest request, CancellationToken ct = default);

    Task<InvitationInfoResponse> GetInvitationInfoAsync(string token, CancellationToken ct = default);
    Task<AuthResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default);

    // Старый вход по SMS (выключен флагом Auth:SmsLoginEnabled).
    Task<SmsSendResponse> SendSmsCodeAsync(SmsSendRequest request, string? requestIp = null, CancellationToken ct = default);
    Task<AuthResponse> VerifySmsCodeAsync(SmsVerifyRequest request, CancellationToken ct = default);
}
