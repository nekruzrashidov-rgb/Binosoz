using FluentValidation;
using Sitora.Domain.DTOs.Auth;
using Sitora.WebApi.Validators.Sales;

namespace Sitora.WebApi.Validators.Auth;

public class SmsSendRequestValidator : AbstractValidator<SmsSendRequest>
{
    public SmsSendRequestValidator()
    {
        RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message);
    }
}

public class SmsVerifyRequestValidator : AbstractValidator<SmsVerifyRequest>
{
    public SmsVerifyRequestValidator()
    {
        RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message);
        RuleFor(x => x.Code).NotEmpty().WithMessage("Укажите код из SMS")
            .Matches("^[0-9]{6}$").WithMessage("Код состоит из 6 цифр");
    }
}

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Укажите почту")
            .EmailAddress().WithMessage("Неверный формат почты").MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Укажите имя").MaximumLength(200);
        RuleFor(x => x.Role).NotEmpty().WithMessage("Укажите роль");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Укажите пароль").MaximumLength(128);
        RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message)
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
