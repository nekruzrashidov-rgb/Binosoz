using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Reviews;

namespace Sitora.Domain.Interfaces;

// Отзывы о домах: покупатель пишет, сайт показывает опубликованные, застройщик отвечает и публикует, суперадмин удаляет.
public interface IReviewService
{
    Task<ReviewResponse> CreateAsync(Guid buildingId, SaveReviewRequest request, CancellationToken ct = default);
    Task<ReviewResponse> UpdateAsync(Guid buildingId, Guid reviewId, SaveReviewRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ReviewResponse>> GetPublishedAsync(Guid buildingId, CancellationToken ct = default);

    Task<IReadOnlyList<ReviewResponse>> GetAllAsync(Guid buildingId, CancellationToken ct = default);
    Task<ReviewResponse> ReplyAsync(Guid buildingId, Guid reviewId, ReplyReviewRequest request, CancellationToken ct = default);
    Task<ReviewResponse> GenerateAiReplyAsync(Guid buildingId, Guid reviewId, CancellationToken ct = default);
    Task<ReviewResponse> SetPublishedAsync(Guid buildingId, Guid reviewId, bool published, CancellationToken ct = default);

    Task<PagedResult<ReviewResponse>> GetPlatformAsync(int page, int pageSize, CancellationToken ct = default);
    Task DeleteAsync(Guid reviewId, CancellationToken ct = default);
}
