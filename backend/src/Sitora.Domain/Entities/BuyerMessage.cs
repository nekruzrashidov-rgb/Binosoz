namespace Sitora.Domain.Entities;

// Сообщение покупателю по одному каналу (кабинет, Telegram, почта) — очередь отправки и журнал для CRM:
// кому, когда, по какому каналу и чем кончилось. Текст готовится сразу, задача Hangfire только отправляет его
// (с повторами при сбое). DedupeKey — защита от дублей: одно и то же напоминание по одному каналу уходит один раз.
public class BuyerMessage : BaseEntity
{
    public const int MaxAttempts = 5;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public Guid? OrganizationId { get; set; }
    public Guid? DealId { get; set; }
    public DateOnly? DueDate { get; set; }

    // Событие (BuyerEvents) и канал (BuyerChannels).
    public string Event { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = BuyerMessageStatus.Pending;
    public string? Error { get; set; }
    public int Attempts { get; set; }
    public DateTime? SentAt { get; set; }

    // Telegram — Markdown; почта — тема, текст и HTML. Без паспортных данных и чужих сделок.
    public string? Subject { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Html { get; set; }

    // Сотрудник, нажавший «Напомнить сейчас»; null — фоновая задача.
    public Guid? CreatedById { get; set; }
    public string? DedupeKey { get; set; }
}

public static class BuyerChannels
{
    public const string InApp = "InApp";
    public const string Telegram = "Telegram";
    public const string Email = "Email";
}

public static class BuyerMessageStatus
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    // Не отправлено и не будет: канал отключён, бот заблокирован, почта не подтверждена.
    public const string Skipped = "Skipped";
}

public static class BuyerEvents
{
    public const string PaymentReminder = Notification.KindReminder;     // за 3 дня
    public const string PaymentDueToday = "payment_due_today";
    public const string PaymentOverdue = Notification.KindOverdue;       // 1-й день просрочки
    public const string PaymentOverdueWeek = "overdue_payment_7";        // через 6 дней после первого — 7-й день
    public const string PaymentManual = "payment_manual";                // «Напомнить сейчас» из CRM
    public const string PaymentReceived = "payment_received";
    public const string BookingCreated = "booking_created";
    public const string BookingExpiring = "booking_expiring";
    public const string BookingReleased = "booking_released";
    public const string DealStage = "deal_stage";
    public const string DocumentAdded = "document_added";
    public const string ChatMessage = "chat_message";

    public static readonly string[] PaymentReminders = [PaymentReminder, PaymentDueToday, PaymentOverdue, PaymentOverdueWeek, PaymentManual];

    // Группа событий для галочек «что присылать».
    public static NotifyEventGroups GroupOf(string evt) => evt switch
    {
        BookingCreated or BookingExpiring or BookingReleased => NotifyEventGroups.Bookings,
        DealStage => NotifyEventGroups.Deal,
        DocumentAdded => NotifyEventGroups.Documents,
        ChatMessage => NotifyEventGroups.Chat,
        _ => NotifyEventGroups.Payments
    };
}

[Flags]
public enum NotifyEventGroups
{
    None = 0,
    Bookings = 1,
    Deal = 2,
    Payments = 4,
    Documents = 8,
    Chat = 16,
    All = Bookings | Deal | Payments | Documents | Chat
}
