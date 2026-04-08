using Ya.Events.WebApi.Enums;

namespace Ya.Events.WebApi.DTOs.Responses;

public record BookingResponse(Guid Id, Guid EventId, BookingStatus Status, DateTime CreatedAt, DateTime? ProcessedAt);
