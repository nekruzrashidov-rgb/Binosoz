using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Auth;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Security;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController(IAuthService auth, IOptions<AuthOptions> options) : ControllerBase
{
    /// <summary>Код для входа на почту — только покупателям (сотрудники входят по паролю). Ответ одинаковый, есть ли такая учётная запись.</summary>
    /// <remarks>Не чаще одного кода в минуту и пяти в час на почту и на IP. Код живёт 10 минут, 5 попыток ввода.</remarks>
    [HttpPost("email/send")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<EmailSendResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EmailSendResponse>> SendEmailCode(EmailSendRequest request, CancellationToken ct) =>
        Ok(await auth.SendEmailCodeAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Проверка кода из почты — вход покупателя. Почта сотрудника — 401 «Сотрудники входят по паролю».</summary>
    [HttpPost("email/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> VerifyEmailCode(EmailVerifyRequest request, CancellationToken ct) =>
        auth.VerifyEmailCodeAsync(request, ct);

    /// <summary>
    /// Регистрация покупателя на сайте застройщика (organization — его короткое имя). Создаёт карточку клиента
    /// и отправляет код на почту; дальше — обычный /auth/email/verify. Телефон из чужой карточки — 409.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<EmailSendResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EmailSendResponse>> Register(RegisterCustomerRequest request, CancellationToken ct) =>
        Ok(await auth.RegisterCustomerAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Сведения о приглашении по токену из письма: организация, роль, имя и почта. Недействительное — 404.</summary>
    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<InvitationInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<InvitationInfoResponse> InvitationInfo(string token, CancellationToken ct) =>
        auth.GetInvitationInfoAsync(token, ct);

    /// <summary>
    /// Принять приглашение по токену из письма: создаётся учётная запись сотрудника и выдаются токены.
    /// Необязательный пароль (от 8 символов, цифра, строчная и заглавная буквы) открывает и вход через /auth/login.
    /// </summary>
    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> AcceptInvitation(AcceptInvitationRequest request, CancellationToken ct) =>
        auth.AcceptInvitationAsync(request, ct);

    /// <summary>
    /// Вход сотрудника (Owner, ObjectAdmin, Manager, Accountant, Content, SuperAdmin) по почте и паролю.
    /// Покупателю — 400 «Используйте вход по коду». 5 неудачных попыток — блокировка на 15 минут.
    /// С включённой 2FA ответ — { requiresTwoFactor: true, tempToken, expiresAt }, токены выдаёт /auth/2fa/verify.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<TwoFactorRequiredResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        return result.TwoFactor is { } twoFactor ? Ok(twoFactor) : Ok(result.Auth);
    }

    /// <summary>Второй шаг входа с 2FA: временный токен из /auth/login и 6 цифр из приложения-аутентификатора.</summary>
    /// <remarks>Токен живёт 5 минут, 5 попыток ввода.</remarks>
    [HttpPost("2fa/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> VerifyTwoFactor(TwoFactorVerifyRequest request, CancellationToken ct) =>
        auth.VerifyTwoFactorAsync(request, ct);

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> Refresh(RefreshRequest request, CancellationToken ct) =>
        auth.RefreshAsync(request, ct);

    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await auth.LogoutAsync(request, ct);
        return NoContent();
    }

    /// <summary>Старый вход покупателя по SMS. Выключен (404), пока Auth:SmsLoginEnabled = false.</summary>
    [HttpPost("sms/send")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<SmsSendResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendSms(SmsSendRequest request, CancellationToken ct) =>
        options.Value.SmsLoginEnabled ? Accepted(await auth.SendSmsCodeAsync(request, ct)) : NotFound();

    [HttpPost("sms/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifySms(SmsVerifyRequest request, CancellationToken ct) =>
        options.Value.SmsLoginEnabled ? Ok(await auth.VerifySmsCodeAsync(request, ct)) : NotFound();

    /// <summary>Текущий пользователь: роли, главная роль, организация, дома и режим «войти как владелец».</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public Task<CurrentUserResponse> Me(CancellationToken ct) =>
        auth.GetCurrentUserAsync(User.GetUserId(), ct);
}
