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

// Покупатель: почта и 6-значный код входа. Слабый код (одинаковые цифры, подряд) проверяет сервис — 422 с подсказкой.
public class CustomerRegisterRequestValidator : AbstractValidator<CustomerRegisterRequest>
{
    public CustomerRegisterRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите имя").MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту")
            .EmailAddress().WithMessage("Неверный формат почты").MaximumLength(256);
        RuleFor(x => x.Phone).MaximumLength(32);
        RuleFor(x => x.Pin).NotEmpty().WithMessage("Придумайте код входа").MaximumLength(32);
        RuleFor(x => x.Organization).MaximumLength(63);
    }
}

public class CustomerEmailRequestValidator : AbstractValidator<CustomerEmailRequest>
{
    public CustomerEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту")
            .EmailAddress().WithMessage("Неверный формат почты").MaximumLength(256);
    }
}

public class CustomerConfirmRequestValidator : AbstractValidator<CustomerConfirmRequest>
{
    public CustomerConfirmRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту").MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().WithMessage("Введите код из письма")
            .Matches("^[0-9]{6}$").WithMessage("Код состоит из 6 цифр");
    }
}

public class CustomerLoginRequestValidator : AbstractValidator<CustomerLoginRequest>
{
    public CustomerLoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту").MaximumLength(256);
        RuleFor(x => x.Pin).NotEmpty().WithMessage("Введите код входа").MaximumLength(32);
    }
}

public class CustomerPinResetRequestValidator : AbstractValidator<CustomerPinResetRequest>
{
    public CustomerPinResetRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту").MaximumLength(256);
        RuleFor(x => x.Code).NotEmpty().WithMessage("Введите код из письма")
            .Matches("^[0-9]{6}$").WithMessage("Код состоит из 6 цифр");
        RuleFor(x => x.NewPin).NotEmpty().WithMessage("Придумайте код входа").MaximumLength(32);
    }
}
