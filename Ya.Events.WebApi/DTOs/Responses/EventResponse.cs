namespace Ya.Events.WebApi.DTOs.Responses;

public record EventResponse(Guid Id, string Title, string? Description, DateTime StartAt, DateTime EndAt);
