using Microsoft.EntityFrameworkCore;
using Sitora.Domain.DTOs.Construction;
using Sitora.Domain.Entities;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Security;

namespace Sitora.Infrastructure.Services;

public class ConstructionService(
    AppDbContext db,
    DealAccess access,
    IFileStorage storage,
    FileLinkSigner links,
    IAuditService audit,
    TimeProvider timeProvider) : IConstructionService
{
    public const long MaxPhotoSize = 10 * 1024 * 1024;

    public async Task<BuildingProgressResponse> GetStagesAsync(Guid buildingId, CancellationToken ct = default)
    {
        if (!await access.CanViewBuildingProgressAsync(buildingId, ct))
            throw new NotFoundException("Дом не найден");

        var stages = await db.ConstructionStages.AsNoTracking()
            .Where(s => s.BuildingId == buildingId)
            .Include(s => s.Photos)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
            .ToListAsync(ct);

        var overall = stages.Count == 0 ? 0 : (int)Math.Round(stages.Average(s => s.Percent), MidpointRounding.AwayFromZero);
        return new BuildingProgressResponse(buildingId, overall, stages.Select(ToResponse).ToList());
    }

    public async Task<StageResponse> CreateStageAsync(Guid buildingId, SaveStageRequest request, CancellationToken ct = default)
    {
        if (!await db.Buildings.AnyAsync(b => b.Id == buildingId, ct))
            throw new NotFoundException("Дом не найден");

        var stage = new ConstructionStage { BuildingId = buildingId };
        Apply(stage, request);
        db.ConstructionStages.Add(stage);
        audit.Record(nameof(ConstructionStage), stage.Id, "Created", null, Snapshot(stage));
        await db.SaveChangesAsync(ct);
        return ToResponse(stage);
    }

    public async Task<StageResponse> UpdateStageAsync(Guid stageId, SaveStageRequest request, CancellationToken ct = default)
    {
        var stage = await db.ConstructionStages.Include(s => s.Photos).FirstOrDefaultAsync(s => s.Id == stageId, ct)
            ?? throw new NotFoundException("Этап стройки не найден");

        var before = Snapshot(stage);
        Apply(stage, request);
        audit.Record(nameof(ConstructionStage), stage.Id, "Updated", before, Snapshot(stage));
        await db.SaveChangesAsync(ct);
        return ToResponse(stage);
    }

    public async Task<PhotoResponse> AddPhotoAsync(Guid stageId, UploadPhotoRequest request, CancellationToken ct = default)
    {
        var buildingId = await db.ConstructionStages.Where(s => s.Id == stageId).Select(s => (Guid?)s.BuildingId).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Этап стройки не найден");

        var (buffer, contentType, extension) = await FileTypes.ReadCheckedAsync(FileTypes.Images,
            request.FileName, request.Length, request.Content, MaxPhotoSize, "PNG, JPG", ct);
        await using var _ = buffer;

        var photo = new ConstructionPhoto
        {
            StageId = stageId,
            ContentType = contentType,
            Caption = string.IsNullOrWhiteSpace(request.Caption) ? null : request.Caption.Trim(),
            TakenAt = request.TakenAt?.ToUniversalTime() ?? timeProvider.GetUtcNow().UtcDateTime
        };
        photo.FileKey = $"construction/{buildingId:N}/{photo.Id:N}{extension}";

        await storage.SaveAsync(photo.FileKey, buffer, ct);
        db.ConstructionPhotos.Add(photo);
        audit.Record(nameof(ConstructionPhoto), photo.Id, "Uploaded", null, new { photo.StageId, photo.Caption, photo.TakenAt });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(photo.FileKey, CancellationToken.None);
            throw;
        }

        return ToResponse(photo);
    }

    private static void Apply(ConstructionStage stage, SaveStageRequest request)
    {
        if (request.Percent is < 0 or > 100)
            throw new BusinessRuleException("Процент готовности должен быть от 0 до 100");
        stage.Name = request.Name.Trim();
        stage.Percent = request.Percent;
        stage.SortOrder = request.SortOrder;
    }

    private static object Snapshot(ConstructionStage s) => new { s.Name, s.Percent, s.SortOrder };

    private StageResponse ToResponse(ConstructionStage s) =>
        new(s.Id, s.BuildingId, s.Name, s.Percent, s.SortOrder, s.UpdatedAt,
            s.Photos.OrderByDescending(p => p.TakenAt).Select(ToResponse).ToList());

    // Ссылку на фото выдаём только тому, кто прошёл проверку доступа к ходу стройки.
    private PhotoResponse ToResponse(ConstructionPhoto p) =>
        new(p.Id, p.StageId, links.CreateLink(FileLinkKind.Photo, p.Id).Url, p.Caption, p.TakenAt);
}
