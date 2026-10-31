namespace Sitora.Domain.DTOs.Ai;

// Проверка документа, удостоверяющего личность. IsValid — документ читается, ФИО совпадает и срок не истёк.
// RawResponse — ответ модели как есть, для разбора спорных случаев.
public record DocumentVerifyResult(
    bool IsValid,
    string? ExtractedFullName,
    DateOnly? ExpiryDate,
    bool NameMatches,
    bool IsExpired,
    string? Issue,
    string RawResponse);

// Банковская квитанция: что удалось прочитать.
public record ReceiptReadResult(
    bool IsReadable,
    decimal? Amount,
    string? Currency,
    DateOnly? PaymentDate,
    string? Description,
    string? BankName,
    string RawResponse);

// Чек и подходящий неоплаченный пункт графика сделки (сумма отличается меньше чем на 1 %, срок — в пределах 30 дней).
// Платёж по нему вносит бухгалтер обычным POST /deals/{id}/payments.
public record ReceiptReadResponse(
    ReceiptReadResult Receipt,
    Guid? SuggestedScheduleItemId,
    DateOnly? SuggestedDueDate,
    decimal? SuggestedAmount,
    string? SuggestedCurrency);

// Файл из multipart-запроса, уже прочитанный контроллером.
public record AiFile(string FileName, string ContentType, long Length, Stream Content);
