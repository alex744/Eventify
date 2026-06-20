using Ya.Events.Domain.ValueObjects;

namespace Ya.Events.Application.DTOs.Bookings;

public record BookingResponse(Guid Id, Guid EventId, BookingStatus Status, DateTime CreatedAt, DateTime? ProcessedAt);
