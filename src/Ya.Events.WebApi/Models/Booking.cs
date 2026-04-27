using Ya.Events.WebApi.Enums;

namespace Ya.Events.WebApi.Models;

/// <summary>
/// Модель бронирования.
/// </summary>
public record Booking
{
    /// <summary>Уникальный идентификатор брони.</summary>
    public Guid Id { get; init; }

    /// <summary>Идентификатор события, к которому относится бронь.</summary>
    public Guid EventId { get; init; }

    /// <summary>Текущий статус брони.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Дата и время создания брони.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Дата и время обработки брони (опционально).</summary>
    public DateTime? ProcessedAt { get; private set; }

    public Booking(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedAt = null)
    {
        Id = id;
        EventId = eventId;
        Status = status;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
    }

    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }
}
