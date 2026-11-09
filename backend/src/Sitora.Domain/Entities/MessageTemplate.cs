namespace Sitora.Domain.Entities;

// Шаблон сообщения клиенту (SMS / WhatsApp) с подстановками {name}, {apt}, {amount}.
// Шаблоны свои у каждой организации.
public class MessageTemplate : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    // payment — о платеже, booking — о брони, welcome — приветствие.
    public string Type { get; set; } = MessageTemplateTypes.Welcome;
}

public static class MessageTemplateTypes
{
    public const string Payment = "payment";
    public const string Booking = "booking";
    public const string Welcome = "welcome";

    public static readonly string[] All = [Payment, Booking, Welcome];
}
