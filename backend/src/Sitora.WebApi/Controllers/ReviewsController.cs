using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sitora.Domain.DTOs.Common;
using Sitora.Domain.DTOs.Reviews;
using Sitora.Domain.Interfaces;
using Sitora.WebApi.Extensions;

namespace Sitora.WebApi.Controllers;

// Отзывы о доме: покупатель со сделкой пишет, сайт показывает опубликованные, руководители и менеджеры дома отвечают и публикуют.
[ApiController]
[Route("api/v1/buildings/{buildingId:guid}/reviews")]
[Produces("application/json")]
public class ReviewsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Опубликованные отзывы о доме — для сайта, без входа. Автор — только имя.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ReviewResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<IReadOnlyList<ReviewResponse>> Published(Guid buildingId, CancellationToken ct) =>
        reviews.GetPublishedAsync(buildingId, ct);

    /// <summary>
    /// Оставить отзыв (rating 1–5, text до 1000 символов). Только покупатель со сделкой в этом доме (не отменённой) — иначе 403;
    /// один отзыв на дом — повтор 409. Отзыв появится на сайте после публикации застройщиком.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReviewResponse>> Create(Guid buildingId, SaveReviewRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await reviews.CreateAsync(buildingId, request, ct));

    /// <summary>Изменить свой отзыв, пока он не опубликован (после — 422).</summary>
    [HttpPut("{reviewId:guid}")]
    [Authorize(Roles = Roles.Customer)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<ReviewResponse> Update(Guid buildingId, Guid reviewId, SaveReviewRequest request, CancellationToken ct) =>
        reviews.UpdateAsync(buildingId, reviewId, request, ct);

    /// <summary>Все отзывы о доме, включая неопубликованные, с черновиком AI и карточкой клиента.</summary>
    [HttpGet("all")]
    [Authorize(Roles = Roles.Sales)]
    public Task<IReadOnlyList<ReviewResponse>> All(Guid buildingId, CancellationToken ct) =>
        reviews.GetAllAsync(buildingId, ct);

    /// <summary>Ответить на отзыв от имени застройщика (до 2000 символов); ответ видят на сайте вместе с отзывом.</summary>
    [HttpPost("{reviewId:guid}/reply")]
    [Authorize(Roles = Roles.Sales)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    public Task<ReviewResponse> Reply(Guid buildingId, Guid reviewId, ReplyReviewRequest request, CancellationToken ct) =>
        reviews.ReplyAsync(buildingId, reviewId, request, ct);

    /// <summary>Черновик ответа от Claude (claude-haiku-5-5) в aiReply; сам не публикуется. Нет ключа Claude — 503.</summary>
    [HttpPost("{reviewId:guid}/ai-reply")]
    [Authorize(Roles = Roles.Sales)]
    [EnableRateLimiting(RateLimitPolicies.Ai)]
    [ProducesResponseType<ReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public Task<ReviewResponse> AiReply(Guid buildingId, Guid reviewId, CancellationToken ct) =>
        reviews.GenerateAiReplyAsync(buildingId, reviewId, ct);

    /// <summary>Опубликовать отзыв на сайте.</summary>
    [HttpPost("{reviewId:guid}/publish")]
    [Authorize(Roles = Roles.Sales)]
    public Task<ReviewResponse> Publish(Guid buildingId, Guid reviewId, CancellationToken ct) =>
        reviews.SetPublishedAsync(buildingId, reviewId, true, ct);

    /// <summary>Скрыть отзыв с сайта.</summary>
    [HttpPost("{reviewId:guid}/hide")]
    [Authorize(Roles = Roles.Sales)]
    public Task<ReviewResponse> Hide(Guid buildingId, Guid reviewId, CancellationToken ct) =>
        reviews.SetPublishedAsync(buildingId, reviewId, false, ct);
}

// Отзывы всей платформы — только суперадмин.
[ApiController]
[Route("api/v1/platform/reviews")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public class PlatformReviewsController(IReviewService reviews) : ControllerBase
{
    /// <summary>Все отзывы платформы с домом и организацией, новые сверху.</summary>
    [HttpGet]
    public Task<PagedResult<ReviewResponse>> List(int page = 1, int pageSize = PageRequest.DefaultPageSize, CancellationToken ct = default) =>
        reviews.GetPlatformAsync(page, pageSize, ct);

    /// <summary>Удалить отзыв навсегда; копия остаётся в журнале (Review / Deleted).</summary>
    [HttpDelete("{reviewId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid reviewId, CancellationToken ct)
    {
        await reviews.DeleteAsync(reviewId, ct);
        return NoContent();
    }
}
