namespace Sitora.Domain.Entities;

// PDF, выданный онлайн по сделке (график платежей, акт сверки): для счётчика «Зелёного эффекта» — сколько документов
// и страниц не пришлось печатать. Сам файл не хранится (он формируется заново при каждом запросе).
public class IssuedPdf : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid DealId { get; set; }
    public IssuedPdfKind Kind { get; set; }
    // Число страниц из самого PDF; null — не удалось определить (такой PDF в страницы не считается).
    public int? PageCount { get; set; }
}

public enum IssuedPdfKind
{
    Schedule,
    Statement
}
