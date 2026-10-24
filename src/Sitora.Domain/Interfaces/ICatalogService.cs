using Sitora.Domain.DTOs.Catalog;
using Sitora.Domain.DTOs.Common;

namespace Sitora.Domain.Interfaces;

public interface ICatalogService
{
    Task<PagedResult<BuildingResponse>> GetBuildingsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<BuildingResponse> GetBuildingAsync(Guid id, CancellationToken ct = default);
    Task<PagedResult<FloorResponse>> GetFloorsAsync(Guid buildingId, int page, int pageSize, CancellationToken ct = default);
    Task<PagedResult<ApartmentResponse>> GetApartmentsAsync(Guid floorId, int page, int pageSize, CancellationToken ct = default);
    Task<ApartmentResponse> GetApartmentAsync(Guid id, CancellationToken ct = default);

    Task<BuildingResponse> CreateBuildingAsync(SaveBuildingRequest request, CancellationToken ct = default);
    Task<BuildingResponse> UpdateBuildingAsync(Guid id, SaveBuildingRequest request, CancellationToken ct = default);
    Task<FloorResponse> CreateFloorAsync(CreateFloorRequest request, CancellationToken ct = default);
    Task<FloorResponse> UpdateFloorAsync(Guid id, UpdateFloorRequest request, CancellationToken ct = default);
    Task<ApartmentResponse> CreateApartmentAsync(CreateApartmentRequest request, CancellationToken ct = default);
    Task<ApartmentResponse> UpdateApartmentAsync(Guid id, UpdateApartmentRequest request, CancellationToken ct = default);
    Task<ApartmentResponse> SetOnRequestAsync(Guid id, bool onRequest, CancellationToken ct = default);

    Task<ImportResult> ImportApartmentsAsync(Guid buildingId, Stream excelFile, CancellationToken ct = default);
}
