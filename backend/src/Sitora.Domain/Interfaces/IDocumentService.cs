using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Documents;

namespace Sitora.Domain.Interfaces;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentResponse>> GetAsync(Guid dealId, CancellationToken ct = default);
    Task<DocumentResponse> UploadAsync(Guid dealId, UploadDocumentRequest request, CancellationToken ct = default);

    // Проверяет право на документ и выдаёт подписанную ссылку на 15 минут.
    Task<FileLinkResponse> CreateDownloadLinkAsync(Guid documentId, CancellationToken ct = default);
}

// Отдача файла по подписанной ссылке; права проверены, когда ссылку выдавали.
public interface IFileLinkService
{
    Task<StoredFile> OpenAsync(string? token, CancellationToken ct = default);
}
