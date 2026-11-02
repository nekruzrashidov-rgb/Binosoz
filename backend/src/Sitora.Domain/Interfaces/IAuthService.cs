using Sitora.Domain.DTOs.Auth;

namespace Sitora.Domain.Interfaces;

public interface IAuthService
{
    // Сотрудники — по паролю (с 2FA, если она включена); покупателю — 400, он входит по коду.
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifyTwoFactorAsync(TwoFactorVerifyRequest request, CancellationToken ct = default);

    // 2FA (TOTP) сотрудника: ключ для приложения, включить и отключить — кодом из приложения.
    Task<TwoFactorSetupResponse> GetTwoFactorSetupAsync(Guid userId, CancellationToken ct = default);
    Task EnableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken ct = default);
    Task DisableTwoFactorAsync(Guid userId, TwoFactorCodeRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);

    // Вход по коду из почты — только для покупателей. Ответ на отправку одинаковый, есть учётная запись или нет.
    Task<EmailSendResponse> SendEmailCodeAsync(EmailSendRequest request, string? requestIp, CancellationToken ct = default);
    Task<AuthResponse> VerifyEmailCodeAsync(EmailVerifyRequest request, CancellationToken ct = default);
    Task<EmailSendResponse> RegisterCustomerAsync(RegisterCustomerRequest request, string? requestIp, CancellationToken ct = default);

    Task<InvitationInfoResponse> GetInvitationInfoAsync(string token, CancellationToken ct = default);
    Task<AuthResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default);

    // Старый вход по SMS (выключен флагом Auth:SmsLoginEnabled).
    Task<SmsSendResponse> SendSmsCodeAsync(SmsSendRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifySmsCodeAsync(SmsVerifyRequest request, CancellationToken ct = default);
}
