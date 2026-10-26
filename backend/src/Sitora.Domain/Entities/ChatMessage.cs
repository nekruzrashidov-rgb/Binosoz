namespace Sitora.Domain.Entities;

public class ChatMessage : BaseEntity
{
    public const int MaxLength = 2000;

    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = null!;

    // Сторона переписки: покупатель или сотрудник. По ней понятно, чьи сообщения отмечать прочитанными.
    public bool FromCustomer { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime? ReadAt { get; set; }
}
