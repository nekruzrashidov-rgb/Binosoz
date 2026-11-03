namespace Sitora.Domain.DTOs.Reviews;

// Отзыв. На сайте — только опубликованные, автор — по имени (первое слово из карточки), без черновика AI и карточки клиента.
// Сотрудникам и суперадмину приходят и IsPublished, AiReply, CustomerId, CustomerFullName; суперадмину — ещё дом и организация.
public record ReviewResponse(
    Guid Id,
    Guid BuildingId,
    int Rating,
    string Text,
    string AuthorName,
    string? OwnerReply,
    DateTime CreatedAt,
    DateTime? RepliedAt,
    bool? IsPublished = null,
    string? AiReply = null,
    Guid? CustomerId = null,
    string? CustomerFullName = null,
    string? BuildingName = null,
    string? OrganizationName = null);

// Rating 1–5, Text — до 1000 символов.
public record SaveReviewRequest(int Rating, string Text);

public record ReplyReviewRequest(string Text);
