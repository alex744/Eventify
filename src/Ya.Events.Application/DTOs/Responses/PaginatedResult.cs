namespace Ya.Events.Application.DTOs.Responses;

public record PaginatedResult<T>(IReadOnlyList<T> Items, int TotalCount, int CurrentPage, int PageSize);