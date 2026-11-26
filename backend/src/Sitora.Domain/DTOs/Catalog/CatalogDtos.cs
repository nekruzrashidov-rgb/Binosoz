using System.Text.Json.Serialization;
using Sitora.Domain.DTOs.Settings;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Catalog;

// Name и Description — на языке запроса (Accept-Language), для tg без перевода описания — русский текст.
public record BuildingResponse(
    Guid Id,
    string Name,
    string NameRu,
    string NameTg,
    string Address,
    int FloorsCount,
    string? Description,
    string? DescriptionRu,
    string? DescriptionTg,
    string? ModelUrl,
    int TotalApartments,
    int FreeApartments,
    int? CompletionYear = null,
    int? PlannedApartments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BuildingEcoResponse? Eco = null,
    // Данные застройщика помечены как демонстрационные (на сайте — бейдж «Демо-данные»).
    bool IsDemo = false,
    // Удобства дома (ключи BuildingAmenities); пустой список в JSON не попадает.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Amenities = null,
    // Условия рассрочки застройщика; не заданы — поля нет.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] InstallmentTermsResponse? Installment = null,
    // Точка дома на карте и высота этажа (м); не заданы — полей нет (на сайте нет окружения, высота — оценка 3,0 м).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Latitude = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? Longitude = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? FloorHeight = null,
    // Английские название и описание (A-10): не заданы — полей нет, сайт на EN показывает русский текст с пометкой.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? NameEn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DescriptionEn = null,
    // Блок ЖК: буква, порядок, контур в плане (м; точка [x, y] или [x, y, r]), место на участке, уступы. Не заданы — полей нет.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? BlockCode = null,
    int SortOrder = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<double[]>? Footprint = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SitePlacement? SitePosition = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<BuildingSetback>? Setbacks = null);

// PUT /buildings/{id}/shape: блок ЖК целиком. Пустой контур — «не блок» (сайт рисует обычный дом), места и уступов тогда нет.
public record SaveBuildingShapeRequest(
    string? BlockCode = null,
    int SortOrder = 0,
    List<double[]>? Footprint = null,
    SitePlacement? SitePosition = null,
    List<BuildingSetback>? Setbacks = null);

// PUT /owner/site-plan: план участка ЖК целиком; пустой контур — плана нет.
public record SaveSitePlanRequest(List<double[]>? Boundary = null, List<SiteZone>? Zones = null);

// PUT /buildings/{id}/location: координаты задаются парой (обе или ни одной), пустое значение — «не задано».
public record SaveBuildingLocationRequest(decimal? Latitude = null, decimal? Longitude = null, decimal? FloorHeight = null);

// PUT /buildings/{id}/amenities: весь список целиком (ключи BuildingAmenities); пустой — удобств на сайте нет.
public record SaveBuildingAmenitiesRequest(List<string>? Amenities = null);

// Эко-паспорт в ответе: только заполненные поля (null в JSON не попадает); весь объект — только если что-то заполнено.
// WallInsulation и Windows — на языке запроса; Ru/Tg — для формы в CRM.
public record BuildingEcoResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] EnergyClass? EnergyClass,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallInsulation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallInsulationRu,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallInsulationTg,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Windows,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WindowsRu,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WindowsTg,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? SolarPanelsKw,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasHeatMeters,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasWaterMeters,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasLedCommonAreas,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasEvCharging,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasRainwaterReuse,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? HasWasteSorting,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? GreenAreaM2,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HeatingType? HeatingType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? EstimatedKwhPerM2Year,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WallInsulationEn = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? WindowsEn = null)
{
    public bool IsEmpty => this == new BuildingEcoResponse(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
}

// PUT /buildings/{id}/eco: весь паспорт целиком; пустые поля — «не заполнено».
public record SaveBuildingEcoRequest(
    EnergyClass? EnergyClass = null,
    string? WallInsulationRu = null,
    string? WallInsulationTg = null,
    string? WindowsRu = null,
    string? WindowsTg = null,
    decimal? SolarPanelsKw = null,
    bool? HasHeatMeters = null,
    bool? HasWaterMeters = null,
    bool? HasLedCommonAreas = null,
    bool? HasEvCharging = null,
    bool? HasRainwaterReuse = null,
    bool? HasWasteSorting = null,
    decimal? GreenAreaM2 = null,
    HeatingType? HeatingType = null,
    decimal? EstimatedKwhPerM2Year = null,
    string? WallInsulationEn = null,
    string? WindowsEn = null);

public record FloorResponse(
    Guid Id,
    Guid BuildingId,
    int Number,
    bool IsCommercial,
    string? PlanImageUrl,
    int TotalApartments,
    int FreeApartments);

public record ApartmentResponse(
    Guid Id,
    Guid FloorId,
    Guid BuildingId,
    int FloorNumber,
    int Number,
    int Rooms,
    decimal Area,
    decimal PricePerM2,
    decimal Price,
    Currency Currency,
    ApartmentStatus Status,
    int PlanSlot,
    string? Model3dId);

public record SaveBuildingRequest(
    string NameRu,
    string NameTg,
    string Address,
    int FloorsCount,
    string? DescriptionRu,
    string? ModelUrl,
    string? DescriptionTg = null,
    int? CompletionYear = null,
    int? PlannedApartments = null,
    string? NameEn = null,
    string? DescriptionEn = null);

public record CreateFloorRequest(Guid BuildingId, int Number, string? PlanImageUrl);

public record UpdateFloorRequest(int Number, string? PlanImageUrl);

// PlanSlot — позиция на плане этажа (0–7), Model3dId — объект квартиры в 3D-модели (необязательно).
public record CreateApartmentRequest(Guid FloorId, int Number, int Rooms, decimal Area, decimal PricePerM2, int PlanSlot, string? Model3dId = null);

public record UpdateApartmentRequest(int Number, int Rooms, decimal Area, decimal PricePerM2, int PlanSlot, string? Model3dId = null);

public record SetOnRequestRequest(bool OnRequest);

public record ImportError(int Row, string Message);

public record ImportResult(int Created, int Updated, IReadOnlyList<ImportError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

// План этажа: SVG, где каждая квартира — <rect id="apt-{apartmentId}">.
public record SaveFloorPlanRequest(string SvgTemplate);
