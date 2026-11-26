using FluentValidation;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.Entities;

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
        RuleFor(x => x.NameEn).MaximumLength(200).WithMessage("Название на английском не длиннее 200 символов");
        RuleFor(x => x.DescriptionEn).MaximumLength(4000).WithMessage("Описание на английском не длиннее 4000 символов");
        RuleFor(x => x.ModelUrl).MaximumLength(500)
            .Must(BeAbsoluteUrl).When(x => !string.IsNullOrWhiteSpace(x.ModelUrl))
            .WithMessage("Ссылка на 3D-модель должна быть полным адресом http(s)");
        RuleFor(x => x.CompletionYear).InclusiveBetween(2000, 2100).When(x => x.CompletionYear is not null)
            .WithMessage("Год сдачи — от 2000 до 2100");
        RuleFor(x => x.PlannedApartments).InclusiveBetween(1, 5000).When(x => x.PlannedApartments is not null)
            .WithMessage("Квартир в доме — от 1 до 5000");
    }

    private static bool BeAbsoluteUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

// Эко-паспорт: числа неотрицательные и в разумных пределах, тексты — до 200 символов.
// Удобства — только ключи из BuildingAmenities.
public class SaveBuildingAmenitiesRequestValidator : AbstractValidator<SaveBuildingAmenitiesRequest>
{
    public SaveBuildingAmenitiesRequestValidator()
    {
        RuleForEach(x => x.Amenities).Must(BuildingAmenities.IsKnown).WithMessage("Неизвестное удобство дома");
    }
}

// Контур в плане (м): 3–64 вершины, точка — [x, y] или [x, y, r], координаты в пределах километра, радиус 0–50 м,
// без самопересечений и с площадью больше нуля (PlanGeometry). Общие правила для блока, уступа и участка.
public static class PolygonRules
{
    public const double MaxCoordinate = 1000, MaxRadius = 50;

    public static bool VertexCount(List<double[]>? ring) => ring is null || ring.Count == 0 || ring.Count is >= PlanGeometry.MinVertices and <= PlanGeometry.MaxVertices;
    public static bool Points(List<double[]>? ring) => ring is null || ring.All(p => p is { Length: 2 or 3 } && p.All(double.IsFinite)
        && Math.Abs(p[0]) <= MaxCoordinate && Math.Abs(p[1]) <= MaxCoordinate && (p.Length == 2 || p[2] is >= 0 and <= MaxRadius));
    private static bool Usable(List<double[]>? ring) => ring is { Count: >= PlanGeometry.MinVertices and <= PlanGeometry.MaxVertices } && Points(ring);
    public static bool Simple(List<double[]>? ring) => !Usable(ring) || PlanGeometry.IsSimple(PlanGeometry.Vertices(ring!));
    public static bool PositiveArea(List<double[]>? ring) => !Usable(ring) || PlanGeometry.Area(PlanGeometry.Vertices(ring!)) > 0.5;

    public static void Apply<T>(IRuleBuilder<T, List<double[]>?> rule) => rule
        .Must(VertexCount).WithMessage("Контур — от 3 до 64 вершин")
        .Must(Points).WithMessage("Точка контура — числа x и y (м, не дальше 1000 м) и необязательный радиус скругления 0–50 м")
        .Must(Simple).WithMessage("Контур не должен пересекать сам себя")
        .Must(PositiveArea).WithMessage("Площадь контура должна быть больше нуля");
}

// Блок ЖК: буква (1–4 латинские буквы или цифры), контур и уступы по правилам PolygonRules, место на участке.
// Уступ внутри контура и разрыв между блоками проверяет сервис (нужна база).
public class SaveBuildingShapeRequestValidator : AbstractValidator<SaveBuildingShapeRequest>
{
    public SaveBuildingShapeRequestValidator()
    {
        RuleFor(x => x.BlockCode).Matches("^[A-Za-z0-9]{1,4}$").When(x => !string.IsNullOrWhiteSpace(x.BlockCode))
            .WithMessage("Буква блока — от 1 до 4 латинских букв или цифр");
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 1000).WithMessage("Порядок блока — от 0 до 1000");
        PolygonRules.Apply(RuleFor(x => x.Footprint));
        RuleFor(x => x.SitePosition).Must(p => p is null || (double.IsFinite(p.X) && double.IsFinite(p.Y) && Math.Abs(p.X) <= 2000 && Math.Abs(p.Y) <= 2000))
            .WithMessage("Место блока на участке — не дальше 2000 м");
        RuleFor(x => x.SitePosition).Must(p => p is null || (double.IsFinite(p.RotationDeg) && Math.Abs(p.RotationDeg) <= 360))
            .WithMessage("Поворот блока — от -360 до 360°");
        RuleFor(x => x.Setbacks).Must(s => s is null || s.Count <= 10).WithMessage("Уступов — не больше 10");
        RuleForEach(x => x.Setbacks).ChildRules(s =>
        {
            s.RuleFor(x => x.FromFloor).InclusiveBetween(2, 100).WithMessage("Уступ начинается со 2-го этажа или выше");
            s.RuleFor(x => x.PlanDepth).InclusiveBetween(4, 14).When(x => x.PlanDepth is not null).WithMessage("Глубина квартир уступа — от 4 до 14 м");
            PolygonRules.Apply(s.RuleFor(x => (List<double[]>?)x.Footprint));
        });
    }
}

// Правило цены: до 20 ступеней, этажи 1–100 (с ≤ по), ступени не пересекаются, цены м² — больше нуля и не больше
// 1 000 000; цена пентхаусов — так же. Деньги — decimal.
public class SavePriceRuleRequestValidator : AbstractValidator<SavePriceRuleRequest>
{
    public SavePriceRuleRequestValidator()
    {
        RuleFor(x => x.Currency).IsInEnum().WithMessage("Валюта — TJS или USD");
        RuleFor(x => x.Steps).Must(s => s is null || s.Count <= 20).WithMessage("Ступеней цены — не больше 20");
        RuleForEach(x => x.Steps).ChildRules(s =>
        {
            s.RuleFor(x => x.FromFloor).InclusiveBetween(1, 100).WithMessage("Этажи ступени — от 1 до 100");
            s.RuleFor(x => x.ToFloor).InclusiveBetween(1, 100).WithMessage("Этажи ступени — от 1 до 100");
            s.RuleFor(x => x).Must(x => x.FromFloor <= x.ToFloor).WithName("toFloor").WithMessage("Этаж «по» не может быть ниже этажа «с»");
            s.RuleFor(x => x.PricePerM2).GreaterThan(0).LessThanOrEqualTo(1_000_000).WithMessage("Цена за м² — больше нуля и не больше 1 000 000");
            s.RuleFor(x => x.PricePerM2To).GreaterThan(0).LessThanOrEqualTo(1_000_000).When(x => x.PricePerM2To is not null)
                .WithMessage("Цена за м² — больше нуля и не больше 1 000 000");
        });
        RuleFor(x => x.Steps).Must(NotOverlap).WithMessage("Ступени цены не должны пересекаться по этажам");
        RuleFor(x => x.PenthousePricePerM2).GreaterThan(0).LessThanOrEqualTo(1_000_000).When(x => x.PenthousePricePerM2 is not null)
            .WithMessage("Цена за м² — больше нуля и не больше 1 000 000");
        RuleFor(x => x.CommercialPricePerM2).GreaterThan(0).LessThanOrEqualTo(1_000_000).When(x => x.CommercialPricePerM2 is not null)
            .WithMessage("Цена за м² — больше нуля и не больше 1 000 000");
        RuleFor(x => x.ParkingSpotPrice).GreaterThan(0).LessThanOrEqualTo(10_000_000).When(x => x.ParkingSpotPrice is not null)
            .WithMessage("Цена машино-места — больше нуля и не больше 10 000 000");
    }

    private static bool NotOverlap(List<Sitora.Domain.Entities.PriceStep>? steps)
    {
        if (steps is null)
            return true;
        var ordered = steps.OrderBy(s => s.FromFloor).ToList();
        return ordered.Zip(ordered.Skip(1)).All(p => p.First.ToFloor < p.Second.FromFloor);
    }
}

// План участка: контур участка и зоны двора известных видов, каждый — по правилам PolygonRules.
public class SaveSitePlanRequestValidator : AbstractValidator<SaveSitePlanRequest>
{
    public static readonly string[] ZoneKinds = ["playground", "football_field", "parking_ramp", "path", "lawn"];

    public SaveSitePlanRequestValidator()
    {
        PolygonRules.Apply(RuleFor(x => x.Boundary));
        RuleFor(x => x.Zones).Must(z => z is null || z.Count <= 40).WithMessage("Зон участка — не больше 40");
        RuleForEach(x => x.Zones).ChildRules(z =>
        {
            z.RuleFor(x => x.Kind).Must(k => ZoneKinds.Contains(k)).WithMessage("Неизвестная зона участка");
            PolygonRules.Apply(z.RuleFor(x => (List<double[]>?)x.Polygon));
        });
    }
}

// Координаты — парой и в пределах WGS 84; высота этажа — от 2,4 до 6 м.
public class SaveBuildingLocationRequestValidator : AbstractValidator<SaveBuildingLocationRequest>
{
    public SaveBuildingLocationRequestValidator()
    {
        RuleFor(x => x).Must(x => (x.Latitude is null) == (x.Longitude is null))
            .WithName("latitude").WithMessage("Укажите широту и долготу вместе");
        RuleFor(x => x.Latitude).InclusiveBetween(-90m, 90m).When(x => x.Latitude is not null)
            .WithMessage("Широта — от -90 до 90");
        RuleFor(x => x.Longitude).InclusiveBetween(-180m, 180m).When(x => x.Longitude is not null)
            .WithMessage("Долгота — от -180 до 180");
        RuleFor(x => x.FloorHeight).InclusiveBetween(2.4m, 6m).When(x => x.FloorHeight is not null)
            .WithMessage("Высота этажа — от 2,4 до 6 м");
    }
}

public class SaveBuildingEcoRequestValidator : AbstractValidator<SaveBuildingEcoRequest>
{
    public SaveBuildingEcoRequestValidator()
    {
        RuleFor(x => x.EnergyClass).IsInEnum().WithMessage("Неизвестный класс энергоэффективности");
        RuleFor(x => x.HeatingType).IsInEnum().WithMessage("Неизвестный тип отопления");
        RuleFor(x => x.WallInsulationRu).MaximumLength(200).WithMessage("Утепление стен — не длиннее 200 символов");
        RuleFor(x => x.WallInsulationTg).MaximumLength(200).WithMessage("Утепление стен — не длиннее 200 символов");
        RuleFor(x => x.WindowsRu).MaximumLength(200).WithMessage("Окна — не длиннее 200 символов");
        RuleFor(x => x.WindowsTg).MaximumLength(200).WithMessage("Окна — не длиннее 200 символов");
        RuleFor(x => x.WallInsulationEn).MaximumLength(200).WithMessage("Утепление стен — не длиннее 200 символов");
        RuleFor(x => x.WindowsEn).MaximumLength(200).WithMessage("Окна — не длиннее 200 символов");
        RuleFor(x => x.SolarPanelsKw).InclusiveBetween(0m, 100_000m).When(x => x.SolarPanelsKw is not null)
            .WithMessage("Мощность солнечных панелей — от 0 до 100 000 кВт");
        RuleFor(x => x.GreenAreaM2).InclusiveBetween(0m, 10_000_000m).When(x => x.GreenAreaM2 is not null)
            .WithMessage("Площадь озеленения — от 0 до 10 000 000 м²");
        RuleFor(x => x.EstimatedKwhPerM2Year).InclusiveBetween(0m, 2_000m).When(x => x.EstimatedKwhPerM2Year is not null)
            .WithMessage("Расчётное потребление — от 0 до 2000 кВт·ч на м² в год");
    }
}

public class CreateFloorRequestValidator : AbstractValidator<CreateFloorRequest>
{
    public CreateFloorRequestValidator()
    {
        RuleFor(x => x.BuildingId).NotEmpty().WithMessage("Укажите дом");
        RuleFor(x => x.Number).InclusiveBetween(-5, 100).WithMessage("Номер этажа должен быть от -5 до 100");
        RuleFor(x => x.PlanImageUrl).MaximumLength(500);
    }
}

public class UpdateFloorRequestValidator : AbstractValidator<UpdateFloorRequest>
{
    public UpdateFloorRequestValidator()
    {
        RuleFor(x => x.Number).InclusiveBetween(-5, 100).WithMessage("Номер этажа должен быть от -5 до 100");
        RuleFor(x => x.PlanImageUrl).MaximumLength(500);
    }
}

public class CreateApartmentRequestValidator : AbstractValidator<CreateApartmentRequest>
{
    public CreateApartmentRequestValidator()
    {
        RuleFor(x => x.FloorId).NotEmpty().WithMessage("Укажите этаж");
        Include(new ApartmentFieldsValidator<CreateApartmentRequest>(
            x => x.Number, x => x.Rooms, x => x.Area, x => x.PricePerM2, x => x.PlanSlot, x => x.Model3dId));
    }
}

public class UpdateApartmentRequestValidator : AbstractValidator<UpdateApartmentRequest>
{
    public UpdateApartmentRequestValidator()
    {
        Include(new ApartmentFieldsValidator<UpdateApartmentRequest>(
            x => x.Number, x => x.Rooms, x => x.Area, x => x.PricePerM2, x => x.PlanSlot, x => x.Model3dId));
    }
}

public class ApartmentFieldsValidator<T> : AbstractValidator<T>
{
    public ApartmentFieldsValidator(
        System.Linq.Expressions.Expression<Func<T, int>> number,
        System.Linq.Expressions.Expression<Func<T, int>> rooms,
        System.Linq.Expressions.Expression<Func<T, decimal>> area,
        System.Linq.Expressions.Expression<Func<T, decimal>> pricePerM2,
        System.Linq.Expressions.Expression<Func<T, int>> planSlot,
        System.Linq.Expressions.Expression<Func<T, string?>> model3dId)
    {
        RuleFor(number).InclusiveBetween(1, 99999).WithMessage("Номер квартиры должен быть от 1 до 99999");
        RuleFor(rooms).InclusiveBetween(0, 10).WithMessage("Комнат должно быть от 0 (студия) до 10");
        // WithMessage относится только к правилу перед ним, поэтому сообщение — у каждой границы.
        const string areaMessage = "Площадь должна быть больше 0 и не больше 1000 м²";
        const string priceMessage = "Цена за м² должна быть больше нуля и не больше 10 000 000";
        RuleFor(area).GreaterThan(0).WithMessage(areaMessage).LessThanOrEqualTo(1000).WithMessage(areaMessage);
        RuleFor(pricePerM2).GreaterThan(0).WithMessage(priceMessage).LessThanOrEqualTo(10_000_000).WithMessage(priceMessage);
        RuleFor(planSlot).InclusiveBetween(0, Apartment.MaxPlanSlot)
            .WithMessage($"Позиция на плане этажа должна быть от 0 до {Apartment.MaxPlanSlot}");
        RuleFor(model3dId).MaximumLength(Apartment.Model3dIdMaxLength)
            .WithMessage($"ID в 3D-модели не длиннее {Apartment.Model3dIdMaxLength} символов");
    }
}
