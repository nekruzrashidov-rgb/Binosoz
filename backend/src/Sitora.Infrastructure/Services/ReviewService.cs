using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sitora.Domain.DTOs.Ai;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Reviews;
using Sitora.Domain.Entities;
using Sitora.Domain.Enums;
using Sitora.Domain.Exceptions;
using Sitora.Domain.Interfaces;
using Sitora.Infrastructure.Data;
using Sitora.Infrastructure.Integrations;

namespace Sitora.Infrastructure.Services;

// Отзывы о домах. Какие дома видны сотруднику, решают фильтры AppDbContext: чужой дом и его отзывы — 404.
// Покупатель — по своим карточкам клиента (Customer.UserId), с сделкой в этом доме.
public class ReviewService(
    AppDbContext db,
    ICurrentUser currentUser,
    IChatModel model,
    IOptions<ClaudeOptions> claude,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<ReviewService> logger) : IReviewService
{
    private const string BuildingNotFound = "Дом не найден";
    private const string ReviewNotFound = "Отзыв не найден";
    private const string AiNotConfigured = "AI-ответ недоступен: не задан ключ Claude (Claude:ApiKey)";

    public async Task<ReviewResponse> CreateAsync(Guid buildingId, SaveReviewRequest request, CancellationToken ct = default)
    {
        var (rating, text) = Validate(request);
        await EnsureBuildingAsync(buildingId, ct);
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");

        // Карточка клиента этого покупателя со сделкой в доме — не отменённой (идёт или завершена).
        var customerId = await db.Deals.AsNoTracking()
            .Where(d => d.Apartment.Floor.BuildingId == buildingId && d.Stage != DealStage.Cancelled && d.Customer.UserId == userId)
            .Select(d => (Guid?)d.CustomerId)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("Оставить отзыв может только покупатель со сделкой в этом доме");
        if (await db.Reviews.AnyAsync(r => r.BuildingId == buildingId && r.Customer.UserId == userId, ct))
            throw new ConflictException("Вы уже оставили отзыв об этом доме. Его можно изменить, пока он не опубликован");

        var review = new Review { BuildingId = buildingId, CustomerId = customerId, Rating = rating, Text = text };
        db.Reviews.Add(review);
        audit.Record(nameof(Review), review.Id, "Created", null, new { review.BuildingId, review.Rating }, await OrganizationOfAsync(buildingId, ct));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.IsUniqueViolation())
        {
            throw new ConflictException("Вы уже оставили отзыв об этом доме");
        }
        return await GetForStaffAsync(review.Id, ct);
    }

    // Свой отзыв — до публикации; чужой не отличить от несуществующего.
    public async Task<ReviewResponse> UpdateAsync(Guid buildingId, Guid reviewId, SaveReviewRequest request, CancellationToken ct = default)
    {
        var (rating, text) = Validate(request);
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Требуется вход");
        var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.BuildingId == buildingId && r.Customer.UserId == userId, ct)
            ?? throw new NotFoundException(ReviewNotFound);
        if (review.IsPublished)
            throw new BusinessRuleException("Опубликованный отзыв изменить нельзя");

        var before = new { review.Rating, review.Text };
        review.Rating = rating;
        review.Text = text;
        audit.Record(nameof(Review), review.Id, "Updated", before, new { review.Rating, review.Text }, await OrganizationOfAsync(buildingId, ct));
        await db.SaveChangesAsync(ct);
        return await GetForStaffAsync(review.Id, ct);
    }

    public async Task<IReadOnlyList<ReviewResponse>> GetPublishedAsync(Guid buildingId, CancellationToken ct = default)
    {
        await EnsureBuildingAsync(buildingId, ct);
        var rows = await db.Reviews.AsNoTracking()
            .Where(r => r.BuildingId == buildingId && r.IsPublished)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new { r.Id, r.BuildingId, r.Rating, r.Text, r.Customer.FullName, r.OwnerReply, r.CreatedAt, r.RepliedAt })
            .ToListAsync(ct);
        return rows.Select(r => new ReviewResponse(r.Id, r.BuildingId, r.Rating, r.Text, AuthorName(r.FullName), r.OwnerReply, r.CreatedAt, r.RepliedAt)).ToList();
    }

    public async Task<IReadOnlyList<ReviewResponse>> GetAllAsync(Guid buildingId, CancellationToken ct = default)
    {
        await EnsureBuildingAsync(buildingId, ct);
        return await StaffQuery(db.Reviews.Where(r => r.BuildingId == buildingId)).ToListAsync(ct);
    }

    public async Task<ReviewResponse> ReplyAsync(Guid buildingId, Guid reviewId, ReplyReviewRequest request, CancellationToken ct = default)
    {
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2000)
            throw new BusinessRuleException("Ответ — от 1 до 2000 символов");
        var review = await FindAsync(buildingId, reviewId, ct);
        review.OwnerReply = text;
        review.RepliedAt = timeProvider.GetUtcNow().UtcDateTime;
        audit.Record(nameof(Review), review.Id, "Replied", null, new { review.OwnerReply }, await OrganizationOfAsync(buildingId, ct));
        await db.SaveChangesAsync(ct);
        return await GetForStaffAsync(review.Id, ct);
    }

    // Черновик ответа от Claude сохраняется в AiReply и не публикуется: его можно взять в ответ или переписать.
    public async Task<ReviewResponse> GenerateAiReplyAsync(Guid buildingId, Guid reviewId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(claude.Value.ApiKey))
            throw new ServiceUnavailableException(AiNotConfigured);
        var review = await FindAsync(buildingId, reviewId, ct);

        const string system = "Ты менеджер застройщика в Таджикистане. Напиши вежливый ответ на отзыв покупателя. " +
                              "Ответь на языке отзыва (русский или таджикский). Максимум 3 предложения. Пиши только сам ответ, без пояснений.";
        var prompt = $"Отзыв: {review.Text}\nРейтинг: {review.Rating}/5";
        var reply = (await model.CompleteAsync(system, [new AiChatMessage("user", prompt)], ct)).Trim();
        if (reply.Length == 0)
            throw new ServiceUnavailableException("AI не прислал ответ, попробуйте ещё раз");

        review.AiReply = reply.Length > 2000 ? reply[..2000] : reply;
        audit.Record(nameof(Review), review.Id, "AiReplyGenerated", null, null, await OrganizationOfAsync(buildingId, ct));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("AI-ответ на отзыв {ReviewId} готов", review.Id);
        return await GetForStaffAsync(review.Id, ct);
    }

    public async Task<ReviewResponse> SetPublishedAsync(Guid buildingId, Guid reviewId, bool published, CancellationToken ct = default)
    {
        var review = await FindAsync(buildingId, reviewId, ct);
        if (review.IsPublished != published)
        {
            review.IsPublished = published;
            audit.Record(nameof(Review), review.Id, published ? "Published" : "Hidden", new { IsPublished = !published }, new { IsPublished = published },
                await OrganizationOfAsync(buildingId, ct));
            await db.SaveChangesAsync(ct);
        }
        return await GetForStaffAsync(review.Id, ct);
    }

    public async Task<PagedResult<ReviewResponse>> GetPlatformAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var p = PageRequest.Normalize(page, pageSize);
        var total = await db.Reviews.CountAsync(ct);
        var items = await StaffQuery(db.Reviews).Skip(p.Skip).Take(p.PageSize).ToListAsync(ct);
        return new PagedResult<ReviewResponse>(items, p.Page, p.PageSize, total);
    }

    // Удаление — только суперадмин; копия отзыва остаётся в журнале.
    public async Task DeleteAsync(Guid reviewId, CancellationToken ct = default)
    {
        var review = await db.Reviews.Include(r => r.Building).FirstOrDefaultAsync(r => r.Id == reviewId, ct)
            ?? throw new NotFoundException(ReviewNotFound);
        db.Reviews.Remove(review);
        audit.Record(nameof(Review), review.Id, "Deleted",
            new { review.BuildingId, review.CustomerId, review.Rating, review.Text, review.OwnerReply, review.IsPublished }, null, review.Building.OrganizationId);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<ReviewResponse> StaffQuery(IQueryable<Review> reviews) =>
        reviews.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ReviewResponse(r.Id, r.BuildingId, r.Rating, r.Text, r.Customer.FullName, r.OwnerReply, r.CreatedAt, r.RepliedAt,
                r.IsPublished, r.AiReply, r.CustomerId, r.Customer.FullName, r.Building.NameRu,
                db.Organizations.Where(o => o.Id == r.Building.OrganizationId).Select(o => o.Name).FirstOrDefault()));

    private async Task<ReviewResponse> GetForStaffAsync(Guid id, CancellationToken ct) =>
        await StaffQuery(db.Reviews.IgnoreQueryFilters().Where(r => r.Id == id)).SingleAsync(ct);

    private async Task<Review> FindAsync(Guid buildingId, Guid reviewId, CancellationToken ct)
    {
        await EnsureBuildingAsync(buildingId, ct);
        return await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId && r.BuildingId == buildingId, ct)
            ?? throw new NotFoundException(ReviewNotFound);
    }

    private async Task EnsureBuildingAsync(Guid buildingId, CancellationToken ct)
    {
        if (!await db.Buildings.AnyAsync(b => b.Id == buildingId, ct))
            throw new NotFoundException(BuildingNotFound);
    }

    private Task<Guid> OrganizationOfAsync(Guid buildingId, CancellationToken ct) =>
        db.Buildings.IgnoreQueryFilters().Where(b => b.Id == buildingId).Select(b => b.OrganizationId).SingleAsync(ct);

    private static (int Rating, string Text) Validate(SaveReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            throw new BusinessRuleException("Оценка — от 1 до 5");
        var text = request.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > Review.MaxTextLength)
            throw new BusinessRuleException($"Текст отзыва — от 1 до {Review.MaxTextLength} символов");
        return (request.Rating, text);
    }

    private static string AuthorName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // В карточках обычно «Фамилия Имя»: на сайте показываем имя, без фамилии.
        return parts.Length >= 2 ? parts[1] : parts.FirstOrDefault() ?? "Покупатель";
    }
}
