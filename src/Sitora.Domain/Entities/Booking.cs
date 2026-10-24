using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;

namespace Sitora.Domain.Entities;

public class Booking : BaseEntity
{
    public static readonly TimeSpan Duration = TimeSpan.FromHours(48);

    public Guid ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    // Бронь оформлена либо на клиента, либо на заявку с сайта.
    public Guid? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public DateTime ExpiresAt { get; set; }
    public BookingStatus Status { get; private set; } = BookingStatus.Active;
    public string? CancelReason { get; private set; }

    // Продление считается от более позднего момента: текущего окончания брони или «сейчас».
    public void Extend(DateTime now)
    {
        EnsureActive();
        ExpiresAt = (ExpiresAt > now ? ExpiresAt : now).Add(Duration);
    }

    public void Cancel(string reason)
    {
        EnsureActive();
        Status = BookingStatus.Cancelled;
        CancelReason = reason;
    }

    public void Expire()
    {
        EnsureActive();
        Status = BookingStatus.Expired;
    }

    public void Convert()
    {
        EnsureActive();
        Status = BookingStatus.Converted;
    }

    private void EnsureActive()
    {
        if (Status != BookingStatus.Active)
            throw new BusinessRuleException("Бронь уже не активна");
    }
}
