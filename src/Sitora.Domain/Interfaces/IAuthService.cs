using Sitora.Domain.DTOs.Auth;

namespace Sitora.Domain.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken ct = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);

    // Вход по коду из почты — для всех ролей. Ответ на отправку одинаковый, есть учётная запись или нет.
    Task<EmailSendResponse> SendEmailCodeAsync(EmailSendRequest request, string? requestIp, CancellationToken ct = default);
    Task<EmailLoginResult> VerifyEmailCodeAsync(EmailVerifyRequest request, CancellationToken ct = default);
    Task<AuthResponse> CompleteSecondFactorAsync(SecondFactorRequest request, CancellationToken ct = default);

    Task<AuthResponse> AcceptInvitationAsync(AcceptInvitationRequest request, CancellationToken ct = default);

    // Старый вход по SMS (выключен флагом Auth:SmsLoginEnabled).
    Task<SmsSendResponse> SendSmsCodeAsync(SmsSendRequest request, CancellationToken ct = default);
    Task<AuthResponse> VerifySmsCodeAsync(SmsVerifyRequest request, CancellationToken ct = default);
}
