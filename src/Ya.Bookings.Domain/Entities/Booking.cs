using Ya.Bookings.Domain.ValueObjects;

namespace Ya.Bookings.Domain.Entities;

/// <summary>
/// Бронирование события.
/// </summary>
public sealed class Booking
{
    /// <summary>
    /// Уникальный идентификатор брони.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Идентификатор события, к которому относится бронь.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    /// Идентификатор пользователя, создавшего бронь.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// Текущий статус брони.
    /// </summary>
    public BookingStatus Status { get; private set; }

    /// <summary>
    /// Дата и время создания брони.
    /// </summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// Дата и время обработки брони (опционально).
    /// </summary>
    public DateTime? ProcessedAt { get; private set; }

    private Booking() { }

    private Booking(
        Guid id,
        Guid eventId,
        Guid userId,
        BookingStatus status,
        DateTime createdAt,
        DateTime? processedAt = null)
    {
        Id = id;
        EventId = eventId;
        UserId = userId;
        Status = status;
        CreatedAt = createdAt;
        ProcessedAt = processedAt;
    }

    /// <summary>
    /// Создает новую бронь со статусом "Pending".
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Новая бронь.</returns>
    public static Booking CreatePending(Guid eventId, Guid userId)
    {
        ThrowIfNotValid(eventId, userId);

        return new Booking(Guid.NewGuid(), eventId, userId, BookingStatus.Pending, DateTime.UtcNow);
    }

    /// <summary>
    /// Подтверждает бронь, устанавливая статус "Confirmed" и дату обработки.
    /// </summary>
    public void Confirm()
    {
        Status = BookingStatus.Confirmed;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Отклоняет бронь, устанавливая статус "Rejected" и дату обработки.
    /// </summary>
    public void Reject()
    {
        Status = BookingStatus.Rejected;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Отменяет бронь, устанавливая статус "Cancelled" и дату обработки.
    /// </summary>
    public void Cancel()
    {
        // Защита от повторной отмены.
        if (Status == BookingStatus.Cancelled)
            return;

        Status = BookingStatus.Cancelled;
        ProcessedAt = DateTime.UtcNow;
    }

    private static void ThrowIfNotValid(Guid eventId, Guid userId)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("Идентификатор события не может быть пустым.", nameof(EventId));

        if (userId == Guid.Empty)
            throw new ArgumentException("Идентификатор пользователя не может быть пустым.", nameof(UserId));
    }
}
