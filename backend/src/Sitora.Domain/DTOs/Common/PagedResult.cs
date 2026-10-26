namespace Sitora.Domain.DTOs.Common;

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public readonly record struct PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    // Неверные значения не считаем ошибкой, а приводим к допустимым.
    public static PageRequest Normalize(int page, int pageSize) =>
        new(Math.Max(page, 1), pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize));
}
