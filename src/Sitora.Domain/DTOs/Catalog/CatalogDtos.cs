using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Catalog;

public record BuildingResponse(
    Guid Id,
    string Name,
    string NameRu,
    string NameTg,
    string Address,
    int FloorsCount,
    string? Description,
    string? ModelUrl,
    int TotalApartments,
    int FreeApartments);

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
    ApartmentStatus Status);

public record SaveBuildingRequest(
    string NameRu,
    string NameTg,
    string Address,
    int FloorsCount,
    string? Description,
    string? ModelUrl);

public record CreateFloorRequest(Guid BuildingId, int Number, string? PlanImageUrl);

public record UpdateFloorRequest(int Number, string? PlanImageUrl);

public record CreateApartmentRequest(Guid FloorId, int Number, int Rooms, decimal Area, decimal PricePerM2);

public record UpdateApartmentRequest(int Number, int Rooms, decimal Area, decimal PricePerM2);

public record SetOnRequestRequest(bool OnRequest);

public record ImportError(int Row, string Message);

public record ImportResult(int Created, int Updated, IReadOnlyList<ImportError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}
