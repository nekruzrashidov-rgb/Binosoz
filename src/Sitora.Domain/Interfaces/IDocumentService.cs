using Sitora.Domain.DTOs.Documents;

namespace Sitora.Domain.Interfaces;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentResponse>> GetAsync(Guid dealId, CancellationToken ct = default);
    Task<DocumentResponse> UploadAsync(Guid dealId, UploadDocumentRequest request, CancellationToken ct = default);
    Task<DocumentFile> DownloadAsync(Guid documentId, CancellationToken ct = default);
}
