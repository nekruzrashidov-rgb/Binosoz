using FluentValidation;
using Sitora.Domain.DTOs.Settings;
using Sitora.Infrastructure.Services;

namespace Sitora.WebApi.Validators.Settings;

// Взнос — процент от 0 до 100, срок — в пределах графика платежей (PaymentScheduleCalculator.MaxMonths).
public class SaveInstallmentSettingsRequestValidator : AbstractValidator<SaveInstallmentSettingsRequest>
{
    public static readonly string MonthsMessage = $"Срок рассрочки — от 1 до {PaymentScheduleCalculator.MaxMonths} месяцев";

    public SaveInstallmentSettingsRequestValidator()
    {
        RuleFor(x => x.DownPaymentMinPct).InclusiveBetween(0m, 100m).When(x => x.DownPaymentMinPct is not null)
            .WithMessage("Первоначальный взнос — от 0 до 100%");
        RuleFor(x => x.MaxMonths).InclusiveBetween(1, PaymentScheduleCalculator.MaxMonths).When(x => x.MaxMonths is not null)
            .WithMessage(MonthsMessage);
    }
}
