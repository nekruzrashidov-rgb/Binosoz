using FluentValidation;
using Sitora.Domain.DTOs.Auth;

namespace Sitora.WebApi.Validators.Auth;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту")
            .EmailAddress().WithMessage("Неверный формат почты")
            .MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().WithMessage("Укажите пароль")
            .MaximumLength(128);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        // Токен может прийти и в cookie: обязательность проверяет контроллер.
        RuleFor(x => x.RefreshToken).MaximumLength(200);
    }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        // Токен может прийти и в cookie: обязательность проверяет контроллер.
        RuleFor(x => x.RefreshToken).MaximumLength(200);
    }
}
