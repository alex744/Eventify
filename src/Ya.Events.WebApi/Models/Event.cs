namespace Ya.Events.WebApi.Models;

public class Event
{
    public Guid Id { get; init; }

    private string _title = string.Empty;
    public string Title
    {
        get => _title;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Название события обязательно.", nameof(Title));
            _title = value;
        }
    }

    public string? Description { get; set; }
    public DateTime StartAt { get; set; }

    private DateTime _endAt;
    public DateTime EndAt
    {
        get => _endAt;
        set
        {
            if (value <= StartAt)
                throw new ArgumentException("Дата окончания должна быть позже даты начала.", nameof(EndAt));
            _endAt = value;
        }
    }

    /// <summary>Общее количество мест на событии.</summary>
    private int _totalSeats;
    public int TotalSeats
    {
        get => _totalSeats;
        private set
        {
            if (value <= 0)
                throw new ArgumentException("Общее количество мест должно быть положительным.", nameof(TotalSeats));
            _totalSeats = value;
        }
    }

    /// <summary>Текущее количество свободных мест.</summary>
    public int AvailableSeats { get; private set; }

    /// <summary>
    /// Создаёт событие с обязательными параметрами.
    /// </summary>
    /// <param name="title">Название события (обязательное).</param>
    /// <param name="startAt">Начало события (обязательное).</param>
    /// <param name="endAt">Окончание события (обязательное и должно быть позже startAt).</param>
    /// <param name="totalSeats">Общее количество мест на событии (обязательное, больше 0).</param>
    /// <param name="description">Описание события (опциональное).</param>    
    public Event(string title, DateTime startAt, DateTime endAt, int totalSeats, string? description = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        TotalSeats = totalSeats;
        AvailableSeats = TotalSeats;
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
            throw new ArgumentOutOfRangeException(nameof(count), "Количество мест должно быть положительным.");

        if (AvailableSeats + count > TotalSeats)
            throw new InvalidOperationException("Невозможно освободить больше мест, чем общее количество.");

        AvailableSeats += count;
    }
}
