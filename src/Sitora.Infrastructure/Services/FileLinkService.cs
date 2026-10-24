using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

public class FileLinkService(AppDbContext db, FileLinkSigner signer, IFileStorage storage) : IFileLinkService
{
    // Одинаковый ответ на подделанную, чужую, просроченную ссылку и на удалённый файл.
    private const string InvalidLink = "Ссылка недействительна или устарела";

    public async Task<StoredFile> OpenAsync(string? token, CancellationToken ct = default)
    {
        if (!signer.TryRead(token, out var kind, out var id))
            throw new NotFoundException(InvalidLink);

        switch (kind)
        {
            case FileLinkKind.Document:
                var document = await db.Documents.AsNoTracking()
                    .Where(d => d.Id == id)
                    .Select(d => new { d.FileKey, d.FileName, d.ContentType })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException(InvalidLink);
                return new StoredFile(document.FileName, document.ContentType, await storage.OpenReadAsync(document.FileKey, ct), Inline: false);

            case FileLinkKind.Photo:
                var photo = await db.ConstructionPhotos.AsNoTracking()
                    .Where(p => p.Id == id)
                    .Select(p => new { p.FileKey, p.ContentType })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException(InvalidLink);
                return new StoredFile(null, photo.ContentType, await storage.OpenReadAsync(photo.FileKey, ct), Inline: true);

            default:
                throw new NotFoundException(InvalidLink);
        }
    }
}
