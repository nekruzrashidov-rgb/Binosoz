using Sitora.Domain.Interfaces;

namespace Sitora.Infrastructure.Jobs;

public class ExpireBookingsJob(IBookingService bookings)
{
    public const string Id = "expire-bookings";
    public const string Schedule = "*/5 * * * *";

    public Task RunAsync(CancellationToken ct) => bookings.ExpireOverdueAsync(ct);
}
