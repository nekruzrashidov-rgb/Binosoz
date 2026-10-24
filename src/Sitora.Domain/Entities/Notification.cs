namespace Sitora.Domain.Entities;

public class Notification : BaseEntity
{
    // null — сообщение в общий канал менеджеров, а не конкретному пользователю.
    public Guid? UserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
}
