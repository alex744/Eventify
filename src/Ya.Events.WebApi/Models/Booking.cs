using Ya.Events.WebApi.Enums;

namespace Ya.Events.WebApi.Models;

/// <summary>
/// Модель бронирования.
/// </summary>
public record Booking(
    Guid Id,                            // Уникальный идентификатор брони.
    Guid EventId,                       // Идентификатор события, к которому относится бронь.
    BookingStatus Status,               // Текущий статус брони.
    DateTime CreatedAt,                 // Дата и время создания брони.
    DateTime? ProcessedAt = null);      // Дата и время обработки брони (опционально).
