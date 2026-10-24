using Sitora.Domain.Enums;

namespace Sitora.Domain.DTOs.Documents;

public record UploadDocumentRequest(DocumentType Type, DateTime? SignedAt, string FileName, string ContentType, long Length, Stream Content);

public record DocumentResponse(
    Guid Id,
    Guid DealId,
    DocumentType Type,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime? SignedAt,
    DateTime CreatedAt);