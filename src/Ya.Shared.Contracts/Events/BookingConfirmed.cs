namespace Ya.Shared.Contracts.Events;

/// <summary>
/// Событие подтверждения брони.
/// Публикуется при успешном подтверждении брони.
/// </summary>
public sealed record BookingConfirmed
{
    /// <summary>
    /// Идентификатор брони
    /// </summary>
    public required Guid BookingId { get; init; }

    /// <summary>
    /// Идентификатор события
    /// </summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// Идентификатор пользователя
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Количество зарезервированных мест
    /// </summary>
    public required int NumberOfSeats { get; init; }

    /// <summary>
    /// Момент подтверждения брони
    /// </summary>
    public required DateTime ConfirmedAt { get; init; }
}