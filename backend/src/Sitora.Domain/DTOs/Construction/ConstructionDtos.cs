namespace Sitora.Domain.DTOs.Construction;

// Url — подписанная ссылка на фото, действует 15 минут (её можно ставить прямо в <img src>).
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

// Для публичного сайта: только названия этапов и проценты, без фото (фото стройки видят покупатели и сотрудники).
public record PublicStageResponse(string Name, int Percent, DateTime UpdatedAt);
public record PublicProgressResponse(int OverallPercent, IReadOnlyList<PublicStageResponse> Stages);

public record SaveStageRequest(string Name, int Percent, int SortOrder);

public record UploadPhotoRequest(string FileName, long Length, Stream Content, string? Caption, DateTime? TakenAt);