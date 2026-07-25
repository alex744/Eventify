namespace Ya.Events.Application.DTOs;

public record EventResponse(Guid Id, string Title, string? Description, DateTime StartAt, DateTime EndAt, int TotalSeats, int AvailableSeats);
