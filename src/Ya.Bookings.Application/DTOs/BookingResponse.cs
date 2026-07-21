using Ya.Bookings.Domain.ValueObjects;

namespace Ya.Bookings.Application.DTOs;

public record BookingResponse(Guid Id, Guid EventId, BookingStatus Status, DateTime CreatedAt, DateTime? ProcessedAt);
