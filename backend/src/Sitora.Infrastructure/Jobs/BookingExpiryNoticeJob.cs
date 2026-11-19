using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Enums;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Services;

namespace Sitora.Infrastructure.Jobs;

// Покупателю в Telegram: «бронь закончится через 6 часов». Раз в 15 минут смотрит брони, которым осталось ≤ 6 часов;
// одно сообщение на срок брони (продлённая бронь предупредит ещё раз перед новым сроком).
public class BookingExpiryNoticeJob(AppDbContext db, BuyerNotifier buyers, TimeProvider timeProvider)
{
    public const string Id = "booking-expiry-notices";
    public const string Schedule = "*/15 * * * *";
    public static readonly TimeSpan Before = TimeSpan.FromHours(6);

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var soon = await db.Bookings.IgnoreQueryFilters().AsNoTracking()
            .Where(b => b.Status == BookingStatus.Active && b.CustomerId != null && b.ExpiresAt > now && b.ExpiresAt <= now.Add(Before))
            .Select(b => b.Id)
            .ToListAsync(ct);
        foreach (var id in soon)
            await buyers.BookingExpiringAsync(id, ct);
        return soon.Count;
    }
}
