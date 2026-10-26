using Sitora.Domain.DTOs.Sales;

namespace Sitora.Domain.Interfaces;

public interface IBookingService
{
    Task<BookingResponse> CreateAsync(Guid apartmentId, CreateBookingRequest request, CancellationToken ct = default);
    Task<BookingResponse> ExtendAsync(Guid id, CancellationToken ct = default);
    Task<BookingResponse> CancelAsync(Guid id, string reason, CancellationToken ct = default);

    // Снимает просроченные брони; возвращает число снятых.
    Task<int> ExpireOverdueAsync(CancellationToken ct = default);
}
