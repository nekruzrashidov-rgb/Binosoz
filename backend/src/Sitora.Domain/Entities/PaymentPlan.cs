using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

public class PaymentPlan : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    public decimal DownPaymentPct { get; set; }
    public int Months { get; set; }
    public int PaymentDay { get; set; }
    public DateOnly StartDate { get; set; }

    public List<PaymentScheduleItem> Items { get; set; } = [];

    public decimal Total => Items.Sum(i => i.AmountDue);

    // Оплаченная сумма закрывает пункты по порядку: сначала самые ранние.
    public void ApplyPayments(decimal totalPaid)
    {
        var left = Math.Max(totalPaid, 0);
        foreach (var item in Items.OrderBy(i => i.DueDate).ThenBy(i => i.Kind))
        {
            item.AmountPaid = Math.Min(item.AmountDue, left);
            left -= item.AmountPaid;
            item.Status = item.AmountPaid == 0 ? ScheduleItemStatus.Pending
                : item.AmountPaid < item.AmountDue ? ScheduleItemStatus.PartiallyPaid
                : ScheduleItemStatus.Paid;
        }
    }
}

public class PaymentScheduleItem : BaseEntity
{
    public Guid PlanId { get; set; }
    public PaymentPlan Plan { get; set; } = null!;

    public ScheduleItemKind Kind { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal AmountDue { get; set; }
    public decimal AmountPaid { get; set; }
    public ScheduleItemStatus Status { get; set; } = ScheduleItemStatus.Pending;
}
