using Sitora.Domain.DTOs.Ai;

namespace Sitora.Domain.Interfaces;

// Чтение документов моделью с распознаванием изображений (Claude). Без ключа Claude — ServiceUnavailableException (503).
public interface IDocumentVerifier
{
    Task<DocumentVerifyResult> VerifyPassportAsync(Stream imageStream, string contentType, string expectedFullName, CancellationToken ct = default);
    Task<ReceiptReadResult> ReadReceiptAsync(Stream imageStream, string contentType, CancellationToken ct = default);
}

// Проверка документов сделки и чтение чеков: доступ к сделке, аудит, сопоставление чека с графиком платежей.
public interface IAiDocumentService
{
    // Уже загруженный документ сделки; ФИО по умолчанию — покупателя сделки.
    Task<DocumentVerifyResult> VerifyStoredAsync(Guid documentId, string? expectedFullName, CancellationToken ct = default);

    // Новый файл, который не сохраняется.
    Task<DocumentVerifyResult> VerifyUploadAsync(Guid dealId, AiFile file, string? expectedFullName, CancellationToken ct = default);

    Task<ReceiptReadResponse> ReadReceiptAsync(Guid dealId, AiFile file, CancellationToken ct = default);
}
