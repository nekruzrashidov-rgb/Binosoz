using FluentValidation;
using Sitora.Domain.DTOs.Catalog;

namespace Sitora.WebApi.Validators.Catalog;

public class SaveBuildingRequestValidator : AbstractValidator<SaveBuildingRequest>
{
    public SaveBuildingRequestValidator()
    {
        RuleFor(x => x.NameRu).NotEmpty().WithMessage("Укажите название на русском").MaximumLength(200);
        RuleFor(x => x.NameTg).NotEmpty().WithMessage("Укажите название на таджикском").MaximumLength(200);
        RuleFor(x => x.Address).NotEmpty().WithMessage("Укажите адрес").MaximumLength(300);
        RuleFor(x => x.FloorsCount).InclusiveBetween(1, 100).WithMessage("Этажность должна быть от 1 до 100");
        RuleFor(x => x.DescriptionRu).MaximumLength(4000).WithMessage("Описание на русском не длиннее 4000 символов");
        RuleFor(x => x.DescriptionTg).MaximumLength(4000).WithMessage("Описание на таджикском не длиннее 4000 символов");
        RuleFor(x => x.ModelUrl).MaximumLength(500)
            .Must(BeAbsoluteUrl).When(x => !string.IsNullOrWhiteSpace(x.ModelUrl))
            .WithMessage("Ссылка на 3D-модель должна быть полным адресом http(s)");
    }

    private static bool BeAbsoluteUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public class CreateFloorRequestValidator : AbstractValidator<CreateFloorRequest>
{
    public CreateFloorRequestValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Укажите дом");
        RuleFor(x => x.Number).InclusiveBetween(0, 100).WithMessage("Номер этажа должен быть от 0 до 100");
        RuleFor(x => x.PlanImageUrl).MaximumLength(500);
    }
}

public class UpdateFloorRequestValidator : AbstractValidator<UpdateFloorRequest>
{
    public UpdateFloorRequestValidator()
    {
        RuleFor(x => x.Number).InclusiveBetween(0, 100).WithMessage("Номер этажа должен быть от 0 до 100");
        RuleFor(x => x.PlanImageUrl).MaximumLength(500);
    }
}

public class CreateApartmentRequestValidator : AbstractValidator<CreateApartmentRequest>
{
    public CreateApartmentRequestValidator()
    {
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Укажите этаж");
        Include(new ApartmentFieldsValidator<CreateApartmentRequest>(x => x.Number, x => x.Rooms, x => x.Area, x => x.PricePerM2));
    }
}

public class UpdateApartmentRequestValidator : AbstractValidator<UpdateApartmentRequest>
{
    public UpdateApartmentRequestValidator()
    {
        Include(new ApartmentFieldsValidator<UpdateApartmentRequest>(x => x.Number, x => x.Rooms, x => x.Area, x => x.PricePerM2));
    }
}

public class ApartmentFieldsValidator<T> : AbstractValidator<T>
{
    public ApartmentFieldsValidator(
        System.Linq.Expressions.Expression<Func<T, int>> number,
        System.Linq.Expressions.Expression<Func<T, int>> rooms,
        System.Linq.Expressions.Expression<Func<T, decimal>> area,
        System.Linq.Expressions.Expression<Func<T, decimal>> pricePerM2)
    {
        RuleFor(number).InclusiveBetween(1, 99999).WithMessage("Номер квартиры должен быть от 1 до 99999");
        RuleFor(rooms).InclusiveBetween(0, 10).WithMessage("Комнат должно быть от 0 (студия) до 10");
        RuleFor(area).GreaterThan(0).LessThanOrEqualTo(1000).WithMessage("Площадь должна быть больше 0 и не больше 1000 м²");
        RuleFor(pricePerM2).GreaterThan(0).LessThanOrEqualTo(10_000_000).WithMessage("Цена за м² должна быть больше нуля");
    }
}
