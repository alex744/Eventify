namespace Ya.Events.Domain.Entities;


/// <summary>
/// Событие в системе.
/// </summary>
public sealed class Event
{
    /// <summary>
    /// Уникальный идентификатор события.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Название события.
    /// </summary>
    public string Title { get; private set; }

    /// <summary>
    /// Описание события (опционально).
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Дата и время начала события.
    /// </summary>
    public DateTime StartAt { get; private set; }

    /// <summary>
    /// Дата и время окончания события.
    /// </summary>
    public DateTime EndAt { get; private set; }

    /// <summary>
    /// Общее количество мест на событии.
    /// </summary>
    public int TotalSeats { get; private set; }

    /// <summary>
    /// Количество доступных мест на событии.
    /// </summary>
    public int AvailableSeats { get; private set; }

    private Event() { Title = null!; }

    private Event(
        Guid id,
        string title,
        DateTime startAt,
        DateTime endAt,
        int totalSeats,
        string? description = null)
    {
        Id = id;
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = TotalSeats;
        Description = description;
    }

    /// <summary>
    /// Создаёт новое событие.
    /// </summary>
    /// <param name="title">Название события.</param>
    /// <param name="startAt">Дата и время начала события.</param>
    /// <param name="endAt">Дата и время окончания события.</param>
    /// <param name="totalSeats">Общее количество мест на событии.</param>
    /// <param name="description">Описание события (опционально).</param>
    /// <returns>Новое событие.</returns>    
    public static Event Create(
        string title,
        DateTime startAt,
        DateTime endAt,
        int totalSeats,
        string? description = null)
    {
        ThrowIfNotValid(title, startAt, endAt, totalSeats);

        return new Event(Guid.NewGuid(), title.Trim(), startAt, endAt, totalSeats, description);
    }

    /// <summary>
    /// Обновляет свойства события.
    /// </summary>
    /// <param name="title">Название события.</param>
    /// <param name="startAt">Дата и время начала события.</param>
    /// <param name="endAt">Дата и время окончания события.</param>
    /// <param name="description">Описание события (опционально).</param>
    public void Update(
       string title,
       DateTime startAt,
       DateTime endAt,
       string? description = null)
    {
        ThrowIfNotValid(title, startAt, endAt, TotalSeats);

        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        Description = description;
    }

    /// <summary>
    /// Пытается зарезервировать указанное количество мест.
    /// </summary>
    /// <param name="count">Количество мест для резервирования (по умолчанию 1).</param>
    /// <returns><c>true</c>, если места успешно зарезервированы; <c>false</c>, если недостаточно свободных мест.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="count"/> меньше или равен нулю.</exception>
    public bool TryReserveSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть положительным.");

        if (AvailableSeats < count)
            return false;

        AvailableSeats -= count;
        return true;
    }

    /// <summary>
    /// Освобождает указанное количество мест (например, при отмене бронирования).
    /// </summary>
    /// <param name="count">Количество освобождаемых мест (по умолчанию 1).</param>
    /// <exception cref="ArgumentOutOfRangeException">Если <paramref name="count"/> меньше или равен нулю.</exception>
    /// <exception cref="InvalidOperationException">Если освобождение приведёт к превышению общего количества мест.</exception>
    public void ReleaseSeats(int count = 1)
    {
        if (count <= 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть больше нуля.");

        if (AvailableSeats + count > TotalSeats)
            throw new InvalidOperationException("Невозможно освободить больше мест, чем общее количество.");

        AvailableSeats += count;
    }

    private static void ThrowIfNotValid(
        string title,
        DateTime startAt,
        DateTime endAt,
        int totalSeats)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Название события обязательно.", nameof(Title));

        if (startAt < DateTime.UtcNow)
            throw new ArgumentException("Дата начала не может быть в прошлом.", nameof(StartAt));

        if (endAt <= startAt)
            throw new ArgumentException("Дата окончания должна быть позже даты начала.", nameof(EndAt));

        if (totalSeats <= 0)
            throw new ArgumentException("Общее количество мест должно быть больше нуля.", nameof(TotalSeats));
    }
}
