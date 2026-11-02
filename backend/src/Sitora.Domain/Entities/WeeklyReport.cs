namespace Sitora.Domain.Entities;

// Еженедельный отчёт владельцу по дому: Excel и PDF за неделю пн–вс хранятся в базе,
// чтобы не зависеть от диска сервера. Один отчёт на дом и неделю; ручная генерация его перезаписывает.
public class WeeklyReport : BaseEntity
{
    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateTime GeneratedAt { get; set; }

    // null — сформирован по расписанию.
    public Guid? GeneratedById { get; set; }

    public byte[] Excel { get; set; } = [];
    public byte[] Pdf { get; set; } = [];
}
