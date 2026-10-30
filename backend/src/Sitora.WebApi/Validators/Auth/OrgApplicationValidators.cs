using FluentValidation;
using Sitora.Domain.DTOs.Auth;

namespace Sitora.WebApi.Validators.Auth;

// Телефон проверяет и приводит к +992… сервис (неверный — 422, как при регистрации покупателя).
public class SubmitApplicationRequestValidator : AbstractValidator<SubmitApplicationRequest>
{
    public SubmitApplicationRequestValidator()
    {
        RuleFor(x => x.CompanyName).Must(v => v?.Trim().Length is >= 2 and <= 200).WithMessage("Название компании — от 2 до 200 символов");
        RuleFor(x => x.OwnerFullName).Must(v => v?.Trim().Length is >= 2 and <= 200).WithMessage("Имя — от 2 до 200 символов");
        RuleFor(x => x.ContactEmail).NotEmpty().WithMessage("Укажите почту")
            .EmailAddress().WithMessage("Неверный формат почты").MaximumLength(256);
        RuleFor(x => x.Comment).MaximumLength(1000).WithMessage("Комментарий — не длиннее 1000 символов");
    }
}

public class ApproveApplicationRequestValidator : AbstractValidator<ApproveApplicationRequest>
{
    public ApproveApplicationRequestValidator()
    {
        RuleFor(x => x.OrganizationSlug).NotEmpty().WithMessage("Укажите короткое имя организации").MaximumLength(63);
    }
}

public class RejectApplicationRequestValidator : AbstractValidator<RejectApplicationRequest>
{
    public RejectApplicationRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Укажите причину отказа").MaximumLength(500);
    }
}
