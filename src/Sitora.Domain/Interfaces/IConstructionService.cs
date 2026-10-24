using Sitora.Domain.DTOs.Construction;

namespace Sitora.Domain.Interfaces;

public interface IConstructionService
{
    Task<BuildingProgressResponse> GetStagesAsync(Guid buildingId, CancellationToken ct = default);
    Task<StageResponse> CreateStageAsync(Guid buildingId, SaveStageRequest request, CancellationToken ct = default);
    Task<StageResponse> UpdateStageAsync(Guid stageId, SaveStageRequest request, CancellationToken ct = default);
    Task<PhotoResponse> AddPhotoAsync(Guid stageId, UploadPhotoRequest request, CancellationToken ct = default);
}
