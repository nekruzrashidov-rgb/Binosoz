namespace Sitora.Domain.DTOs.Construction;

public record PhotoResponse(Guid Id, Guid StageId, string Url, string? Caption, DateTime TakenAt);

public record StageResponse(
    Guid Id,
    Guid BuildingId,
    string Name,
    int Percent,
    int SortOrder,
    DateTime UpdatedAt,
    IReadOnlyList<PhotoResponse> Photos);

// Общая готовность дома — среднее по этапам.
public record BuildingProgressResponse(Guid BuildingId, int OverallPercent, IReadOnlyList<StageResponse> Stages);

public record SaveStageRequest(string Name, int Percent, int SortOrder);

public record UploadPhotoRequest(string FileName, long Length, Stream Content, string? Caption, DateTime? TakenAt);

public record PhotoFile(string ContentType, Stream Content);
