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
    /// <summary>Код для входа на почту — для всех ролей. Ответ одинаковый, есть ли такая учётная запись.</summary>
    /// <remarks>Не чаще одного кода в минуту и пяти в час на почту и на IP. Код живёт 10 минут, 5 попыток ввода.</remarks>
    [HttpPost("email/send")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Sms)]
    [ProducesResponseType<EmailSendResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EmailSendResponse>> SendEmailCode(EmailSendRequest request, CancellationToken ct) =>
        Accepted(await auth.SendEmailCodeAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), ct));

    /// <summary>Проверка кода из почты: токены (200) или запрос второго фактора для создателя платформы (202).</summary>
    [HttpPost("email/verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SecondFactorChallenge>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyEmailCode(EmailVerifyRequest request, CancellationToken ct)
    {
        var result = await auth.VerifyEmailCodeAsync(request, ct);
        return result.Challenge is { } challenge ? Accepted(challenge) : Ok(result.Auth);
    }

    /// <summary>Второй фактор создателя платформы: пароль или код из приложения-аутентификатора.</summary>
    [HttpPost("second-factor")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> SecondFactor(SecondFactorRequest request, CancellationToken ct) =>
        auth.CompleteSecondFactorAsync(request, ct);

    /// <summary>Принять приглашение по токену из письма: создаётся учётная запись сотрудника и выдаются токены.</summary>
    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public Task<AuthResponse> AcceptInvitation(AcceptInvitationRequest request, CancellationToken ct) =>
        auth.AcceptInvitationAsync(request, ct);

    /// <summary>Вход сотрудника по паролю (если включён Auth:PasswordLoginEnabled). Создателю платформы закрыт.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) =>
        options.Value.PasswordLoginEnabled ? Ok(await auth.LoginAsync(request, ct)) : NotFound();

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
