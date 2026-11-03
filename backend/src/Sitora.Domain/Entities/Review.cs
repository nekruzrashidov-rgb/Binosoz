namespace Sitora.Domain.Entities;

// Отзыв покупателя о доме: один на покупателя и дом, только при сделке в этом доме (не отменённой).
// Покупатель правит отзыв, пока его не опубликовали. AiReply — черновик ответа от Claude, сам не публикуется;
// OwnerReply — ответ застройщика, который видят на сайте.
public class Review : BaseEntity
{
    public const int MaxTextLength = 1000;

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? OwnerReply { get; set; }
    public string? AiReply { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? RepliedAt { get; set; }
}
