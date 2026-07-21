namespace Ya.Events.Application.DTOs;

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int CurrentPage, int PageSize);