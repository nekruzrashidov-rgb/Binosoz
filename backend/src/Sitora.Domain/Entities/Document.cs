using Sitora.Domain.Enums;

namespace Sitora.Domain.Entities;

public class Document : BaseEntity
{
    public Guid DealId { get; set; }
    public Deal Deal { get; set; } = null!;

    public DocumentType Type { get; set; }

    public string FileKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime? SignedAt { get; set; }
    public Guid? UploadedById { get; set; }
    // Страниц в PDF (из самого файла при загрузке); null — не PDF или число не определилось (тогда не считается).
    public int? PageCount { get; set; }
}
