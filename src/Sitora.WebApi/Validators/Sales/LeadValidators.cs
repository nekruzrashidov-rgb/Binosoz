using FluentValidation;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;

namespace Sitora.WebApi.Validators.Sales;

public class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите имя").MaximumLength(200);
        RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message);
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public class UpdateLeadRequestValidator : AbstractValidator<UpdateLeadRequest>
{
    public UpdateLeadRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public static class PhoneRules
{
    public const string Message = "Неверный номер телефона, пример: +992 90 123 45 67";

    public static bool IsValid(string? phone) => PhoneNumber.Normalize(phone) is not null;
}
