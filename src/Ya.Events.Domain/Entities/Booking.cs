using Ya.Events.Domain.ValueObjects;

namespace Ya.Events.Domain.Entities;

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

    /// <summary>Ссылка на событие (опционально).</summary>
    public Event? Event { get; private set; }

    private Booking() { }

    private Booking(Guid id, Guid eventId, BookingStatus status, DateTime createdAt, DateTime? processedAt = null)
    {
        Id = id;
        EventId = eventId;
        Status = status;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
    }

    public static Booking CreatePending(Guid eventId)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Идентификатор события не может быть пустым.", nameof(EventId));

        return new Booking(Guid.NewGuid(), eventId, BookingStatus.Pending, DateTime.UtcNow);
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
