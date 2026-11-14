using FluentValidation;
using Sitora.Domain.DTOs.Green;

namespace Sitora.WebApi.Validators.Green;

// Тарифы и коэффициенты неотрицательные и в разумных пределах; у сравнения и у CO₂ обязателен источник цифры.
public class SaveGreenSettingsRequestValidator : AbstractValidator<SaveGreenSettingsRequest>
{
    public SaveGreenSettingsRequestValidator()
    {
        RuleFor(x => x.TariffKwhTjs).InclusiveBetween(0m, 100m).When(x => x.TariffKwhTjs is not null)
            .WithMessage("Тариф — от 0 до 100 TJS за кВт·ч");
        RuleFor(x => x.BaselineKwhPerM2Year).InclusiveBetween(0m, 2_000m).When(x => x.BaselineKwhPerM2Year is not null)
            .WithMessage("Потребление типового дома — от 0 до 2000 кВт·ч на м² в год");
        RuleFor(x => x.BaselineSource).NotEmpty().When(x => x.BaselineKwhPerM2Year is not null)
            .WithMessage("Укажите источник цифры типового дома");
        RuleFor(x => x.BaselineSource).MaximumLength(500).WithMessage("Источник — не длиннее 500 символов");
        RuleFor(x => x.Co2KgPerKgPaper).InclusiveBetween(0m, 100m).When(x => x.Co2KgPerKgPaper is not null)
            .WithMessage("Коэффициент CO₂ — от 0 до 100 кг на кг бумаги");
        RuleFor(x => x.Co2Source).NotEmpty().When(x => x.Co2KgPerKgPaper is not null)
            .WithMessage("Укажите источник коэффициента CO₂");
        RuleFor(x => x.Co2Source).MaximumLength(500).WithMessage("Источник — не длиннее 500 символов");
    }
}
