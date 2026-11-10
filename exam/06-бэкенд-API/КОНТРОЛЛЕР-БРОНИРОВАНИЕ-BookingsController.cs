using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.WebApi.Extensions;
using Sitora.Domain.DTOs.Sales;
using Sitora.Domain.Interfaces;

namespace Sitora.WebApi.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class BookingsController(IBookingService bookings) : ControllerBase
{
    /// <summary>Бронь на 48 часов. Гость указывает имя и телефон, менеджер может указать клиента.</summary>
    [HttpPost("apartments/{apartmentId:guid}/bookings")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.PublicForms)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BookingResponse>> Create(Guid apartmentId, CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookings.CreateAsync(apartmentId, request, ct);
        return StatusCode(StatusCodes.Status201Created, booking);
    }

    [HttpPost("bookings/{id:guid}/extend")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public Task<BookingResponse> Extend(Guid id, CancellationToken ct) =>
        bookings.ExtendAsync(id, ct);

    [HttpPost("bookings/{id:guid}/cancel")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<BookingResponse>(StatusCodes.Status200OK)]
    public Task<BookingResponse> Cancel(Guid id, CancelBookingRequest request, CancellationToken ct) =>
        bookings.CancelAsync(id, request.Reason, ct);
}
