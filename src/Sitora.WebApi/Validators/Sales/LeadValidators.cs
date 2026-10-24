using FluentValidation;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Sales;

namespace Sitora.WebApi.Validators.Sales;

public class CreateLeadRequestValidator : AbstractValidator<CreateLeadRequest>
{
    public CreateLeadRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите имя").MaximumLength(200);
        RuleFor(x => x.Phone).Must(p => PhoneNumber.Normalize(p) is not null)
            .WithMessage("Неверный номер телефона, пример: +992 90 123 45 67");
        RuleFor(x => x.Source).IsInEnum();
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
