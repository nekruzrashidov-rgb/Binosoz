using Microsoft.EntityFrameworkCore;
using Sitora.Domain.Common;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

// Проверка документов сделки и чтение чеков с помощью ИИ. Сама модель — за IDocumentVerifier;
// здесь — доступ к сделке (чужая — 404), запись в аудит и подбор пункта графика под сумму из чека.
// Результат ИИ — подсказка сотруднику: ничего не меняется в сделке и платежах без его действия.
public class AiDocumentService(
    AppDbContext db,
    DealAccess access,
    IFileStorage storage,
    IDocumentVerifier verifier,
    IAuditService audit,
    TimeProvider timeProvider) : IAiDocumentService
{
    // Сумма из чека считается совпавшей, если отличается от остатка по пункту графика меньше чем на 1 %.
    private const decimal AmountTolerance = 0.01m;
    private const int DueDateWindowDays = 30;

    public async Task<DocumentVerifyResult> VerifyStoredAsync(Guid documentId, string? expectedFullName, CancellationToken ct = default)
    {
        var document = await db.Documents.AsNoTracking().Where(d => d.Id == documentId)
            .Select(d => new { d.Id, d.DealId, d.FileKey, d.ContentType })
            .FirstOrDefaultAsync(ct);
        if (document is null || !await access.CanViewAsync(document.DealId, ct))
            throw new NotFoundException("Документ не найден");

        var name = await ExpectedNameAsync(document.DealId, expectedFullName, ct);
        DocumentVerifyResult result;
        await using (var file = await storage.OpenReadAsync(document.FileKey, ct))
            result = await verifier.VerifyPassportAsync(file, document.ContentType, name, ct);

        // В аудит — только вывод проверки, без данных с документа.
        audit.Record(nameof(Document), document.Id, "AiVerified", null,
            new { result.IsValid, result.NameMatches, result.IsExpired, result.Issue });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<DocumentVerifyResult> VerifyUploadAsync(Guid dealId, AiFile file, string? expectedFullName, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        var name = await ExpectedNameAsync(dealId, expectedFullName, ct);
        var result = await verifier.VerifyPassportAsync(file.Content, file.ContentType, name, ct);

        // Файл не сохраняется, поэтому запись — на сделку.
        audit.Record(nameof(Deal), dealId, "AiDocumentVerified", null,
            new { file.FileName, result.IsValid, result.NameMatches, result.IsExpired, result.Issue });
        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<ReceiptReadResponse> ReadReceiptAsync(Guid dealId, AiFile file, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        var receipt = await verifier.ReadReceiptAsync(file.Content, file.ContentType, ct);
        if (!receipt.IsReadable || receipt.Amount is not { } amount)
            return new ReceiptReadResponse(receipt, null, null, null, null);

        var currency = await db.Deals.Where(d => d.Id == dealId).Select(d => d.Currency).SingleAsync(ct);
        // Чек в другой валюте с графиком напрямую не сравнить: курс на дату оплаты бухгалтер укажет сам.
        if (receipt.Currency is { } code && !string.Equals(code, currency.ToString(), StringComparison.OrdinalIgnoreCase))
            return new ReceiptReadResponse(receipt, null, null, null, null);

        var paidOn = receipt.PaymentDate ?? DushanbeTime.Today(timeProvider.GetUtcNow().UtcDateTime);
        var from = paidOn.AddDays(-DueDateWindowDays);
        var to = paidOn.AddDays(DueDateWindowDays);
        var open = await db.PaymentScheduleItems.AsNoTracking()
            .Where(i => i.Plan.DealId == dealId && i.AmountPaid < i.AmountDue && i.DueDate >= from && i.DueDate <= to)
            .Select(i => new { i.Id, i.DueDate, Left = i.AmountDue - i.AmountPaid })
            .ToListAsync(ct);

        var match = open
            .Where(i => Math.Abs(i.Left - amount) < i.Left * AmountTolerance)
            .OrderBy(i => Math.Abs(i.DueDate.DayNumber - paidOn.DayNumber))
            .FirstOrDefault();
        return match is null
            ? new ReceiptReadResponse(receipt, null, null, null, null)
            : new ReceiptReadResponse(receipt, match.Id, match.DueDate, match.Left, currency.ToString());
    }

    private async Task<string> ExpectedNameAsync(Guid dealId, string? expectedFullName, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(expectedFullName))
            return expectedFullName.Trim();
        return await db.Deals.Where(d => d.Id == dealId).Select(d => d.Customer.FullName).SingleAsync(ct);
    }
}
