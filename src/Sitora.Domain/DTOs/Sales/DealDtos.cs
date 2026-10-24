using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

public record CreateDealRequest(
    Guid ApartmentId,
    Guid CustomerId,
    DealStage? Stage,
    Currency? Currency,
    decimal? Discount,
    string? DiscountReason);

public record ChangeDealStageRequest(DealStage Stage, string? Reason);

public record DealResponse(
    Guid Id,
    Guid ApartmentId,
    int ApartmentNumber,
    string BuildingName,
    Guid CustomerId,
    string CustomerName,
    Guid ManagerId,
    DealStage Stage,
    string? StageReason,
    decimal Price,
    decimal Discount,
    string? DiscountReason,
    decimal FinalPrice,
    Currency Currency,
    Guid? BookingId,
    DateTime CreatedAt,
    DateTime UpdatedAt);
