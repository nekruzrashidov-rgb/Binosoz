using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Sales;

// Гость указывает имя и телефон; менеджер может вместо них указать клиента.
public record CreateBookingRequest(string? Name, string? Phone, Guid? CustomerId, string? Comment);

public record CancelBookingRequest(string Reason);

public record BookingResponse(
    Guid Id,
    Guid ApartmentId,
    int ApartmentNumber,
    Guid? CustomerId,
    Guid? LeadId,
    DateTime ExpiresAt,
    BookingStatus Status,
    DateTime CreatedAt);
