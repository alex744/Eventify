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
                throw new ArgumentException("Название события не может быть пустым.");
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
                throw new ArgumentException("Дата окончания должна быть позже даты начала.");
            _endAt = value;
        }
    }

    /// <summary>
    /// Создаёт событие с обязательными параметрами.
    /// </summary>
    /// <param name="title">Название события (обязательное).</param>
    /// <param name="startAt">Начало события (обязательное).</param>
    /// <param name="endAt">Окончание события (обязательное и должно быть позже startAt).</param>
    /// <param name="description">Описание события (опциональное).</param>    
    public Event(string title, DateTime startAt, DateTime endAt, string? description = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        StartAt = startAt;
        EndAt = endAt;
        Description = description;
    }
}
