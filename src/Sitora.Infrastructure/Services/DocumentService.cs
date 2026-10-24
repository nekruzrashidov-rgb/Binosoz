using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sitora.Domain.DTOs.Documents;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;

namespace Sitora.Infrastructure.Services;

public class DocumentService(
    AppDbContext db,
    DealAccess access,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditService audit,
    INotificationSender notifications,
    ILogger<DocumentService> logger) : IDocumentService
{
    public const long MaxFileSize = 20 * 1024 * 1024;

    public async Task<IReadOnlyList<DocumentResponse>> GetAsync(Guid dealId, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        return await db.Documents.AsNoTracking()
            .Where(d => d.DealId == dealId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DocumentResponse(d.Id, d.DealId, d.Type, d.FileName, d.ContentType, d.SizeBytes, d.SignedAt, d.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<DocumentResponse> UploadAsync(Guid dealId, UploadDocumentRequest request, CancellationToken ct = default)
    {
        if (!await access.CanViewAsync(dealId, ct))
            throw new NotFoundException("Сделка не найдена");

        var (buffer, contentType, extension) = await FileTypes.ReadCheckedAsync(FileTypes.Documents,
            request.FileName, request.Length, request.Content, MaxFileSize, "PDF, PNG, JPG, DOCX", ct);
        await using var _ = buffer;

        var document = new Document
        {
            DealId = dealId,
            Type = request.Type,
            FileName = SafeFileName(request.FileName),
            ContentType = contentType,
            SizeBytes = buffer.Length,
            SignedAt = request.SignedAt,
            UploadedById = currentUser.UserId
        };
        document.FileKey = $"documents/{dealId:N}/{document.Id:N}{extension}";

        await storage.SaveAsync(document.FileKey, buffer, ct);
        db.Documents.Add(document);
        audit.Record(nameof(Document), document.Id, "Uploaded", null,
            new { document.DealId, document.Type, document.FileName, document.SizeBytes, document.SignedAt });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(document.FileKey, CancellationToken.None);
            throw;
        }

        await NotifyCustomerAsync(dealId, $"Добавлен документ: {document.FileName}", ct);
        return new DocumentResponse(document.Id, dealId, document.Type, document.FileName, document.ContentType,
            document.SizeBytes, document.SignedAt, document.CreatedAt);
    }

    public async Task<DocumentFile> DownloadAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await db.Documents.AsNoTracking().FirstOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null || !await access.CanViewAsync(document.DealId, ct))
            throw new NotFoundException("Документ не найден");

        var stream = await storage.OpenReadAsync(document.FileKey, ct);
        return new DocumentFile(document.FileName, document.ContentType, stream);
    }

    private async Task NotifyCustomerAsync(Guid dealId, string text, CancellationToken ct)
    {
        var userId = await db.Deals.Where(d => d.Id == dealId).Select(d => d.Customer.UserId).SingleAsync(ct);
        if (userId is null)
            return;
        try
        {
            await notifications.SendAsync(userId, text, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось уведомить покупателя о документе");
        }
    }

    private static string SafeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName);
        name = new string(name.Where(c => !char.IsControl(c) && !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
        return name.Length switch
        {
            0 => "document",
            > 200 => name[^200..],
            _ => name
        };
    }
}
