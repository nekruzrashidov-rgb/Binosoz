namespace Sitora.Domain.Entities;

// Уведомление пользователю: копия сообщения в Telegram (Channel = Telegram) или уведомление в приложении (InApp).
// Все уведомления пользователя видны в колокольчике.
public class Notification : BaseEntity
{
    public const string InAppChannel = "InApp";
    public const string KindOverdue = "overdue_payment";
    public const string KindReminder = "payment_reminder";

    // null — сообщение в общий канал менеджеров, а не конкретному пользователю.
    public Guid? UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }

    // У напоминаний о платежах: вид, сделка и срок пункта графика — по ним не шлём одно и то же дважды.
    public string? Kind { get; set; }
    public Guid? DealId { get; set; }
    public DateOnly? DueDate { get; set; }
}
