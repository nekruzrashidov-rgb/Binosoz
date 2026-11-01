using FluentValidation;
using Sitora.Domain.DTOs.Cash;

namespace Sitora.WebApi.Validators.Cash;

public class CreateCashTransactionRequestValidator : AbstractValidator<CreateCashTransactionRequest>
{
    public CreateCashTransactionRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Сумма должна быть больше нуля")
            .LessThanOrEqualTo(1_000_000_000m).WithMessage("Слишком большая сумма");
        RuleFor(x => x.Amount).Must(a => decimal.Round(a, 2) == a).WithMessage("Сумма — не больше двух знаков после запятой");
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Direction).IsInEnum();
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).NotEmpty().WithMessage("Укажите основание операции").MaximumLength(500);
        RuleFor(x => x.OperationDate).NotEmpty().WithMessage("Укажите дату операции");
    }
}

public class ReverseCashTransactionRequestValidator : AbstractValidator<ReverseCashTransactionRequest>
{
    public ReverseCashTransactionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Укажите причину сторно").MaximumLength(500);
    }
}
