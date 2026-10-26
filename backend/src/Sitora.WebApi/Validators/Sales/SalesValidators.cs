using FluentValidation;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Enums;

namespace Sitora.WebApi.Validators.Sales;

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        When(x => x.CustomerId is null, () =>
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Укажите имя").MaximumLength(200);
            RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message);
        });
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}

public class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Укажите причину отмены брони").MaximumLength(500);
    }
}

public class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Укажите ФИО").MaximumLength(200);
        RuleFor(x => x.Phone).Must(PhoneRules.IsValid).WithMessage(PhoneRules.Message);
        RuleFor(x => x.Email).EmailAddress().WithMessage("Неверный формат почты").MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PassportData).MaximumLength(500);
    }
}

public class CreateDealRequestValidator : AbstractValidator<CreateDealRequest>
{
    public CreateDealRequestValidator()
    {
        RuleFor(x => x.ApartmentId).NotEmpty().WithMessage("Укажите квартиру");
        RuleFor(x => x.CustomerId).NotEmpty().WithMessage("Укажите клиента");
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).WithMessage("Скидка не может быть отрицательной");
        RuleFor(x => x.DiscountReason).NotEmpty().WithMessage("Укажите причину скидки")
            .When(x => x.Discount > 0);
        RuleFor(x => x.DiscountReason).MaximumLength(500);
    }
}

public class ChangeDealStageRequestValidator : AbstractValidator<ChangeDealStageRequest>
{
    public ChangeDealStageRequestValidator()
    {
        RuleFor(x => x.Stage).IsInEnum();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Для отмены сделки укажите причину")
            .When(x => x.Stage == DealStage.Cancelled);
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
