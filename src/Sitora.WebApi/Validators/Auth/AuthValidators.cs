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
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Укажите refresh-токен").MaximumLength(200);
    }
}

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage("Укажите refresh-токен").MaximumLength(200);
    }
}
