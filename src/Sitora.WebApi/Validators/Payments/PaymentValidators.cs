using FluentValidation;
using Sitora.Domain.DTOs.Payments;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Validators.Payments;

public class CreatePlanRequestValidator : AbstractValidator<CreatePlanRequest>
{
    public CreatePlanRequestValidator()
    {
        RuleFor(x => x.DownPaymentPct).GreaterThanOrEqualTo(0).LessThan(100)
            .WithMessage("Первоначальный взнос должен быть от 0 до 100% (не включая 100)");
        RuleFor(x => x.Months).InclusiveBetween(1, PaymentScheduleCalculator.MaxMonths)
            .WithMessage($"Срок должен быть от 1 до {PaymentScheduleCalculator.MaxMonths} месяцев");
        RuleFor(x => x.PaymentDay).InclusiveBetween(1, PaymentScheduleCalculator.MaxPaymentDay)
            .WithMessage($"День платежа должен быть от 1 до {PaymentScheduleCalculator.MaxPaymentDay}");
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("Укажите дату начала графика");
    }
}

public class CreatePaymentRequestValidator : AbstractValidator<CreatePaymentRequest>
{
    public CreatePaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Сумма платежа должна быть больше нуля")
            .LessThanOrEqualTo(1_000_000_000m).WithMessage("Слишком большая сумма");
        RuleFor(x => x.Amount).Must(a => decimal.Round(a, 2) == a).WithMessage("Сумма — не больше двух знаков после запятой");
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.PaidAt).NotEmpty().WithMessage("Укажите дату оплаты");
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

public class ReversePaymentRequestValidator : AbstractValidator<ReversePaymentRequest>
{
    public ReversePaymentRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Укажите причину сторно").MaximumLength(500);
    }
}

public class SetRateRequestValidator : AbstractValidator<SetRateRequest>
{
    public SetRateRequestValidator()
    {
        RuleFor(x => x.Currency).Equal(Currency.USD).WithMessage("Курс задаётся только для USD");
        RuleFor(x => x.RateToTjs).GreaterThan(0).LessThanOrEqualTo(1000).WithMessage("Курс должен быть больше 0 и не больше 1000");
        RuleFor(x => x.Date).NotEmpty().WithMessage("Укажите дату курса");
    }
}
