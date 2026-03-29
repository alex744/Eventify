namespace Ya.Events.WebApi.Exceptions;

public class NotFoundException : Exception
{
    public object? Id { get; }
    public string? EntityName { get; }

    public NotFoundException() { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string message, Exception inner) : base(message, inner) { }

    public NotFoundException(string entityName, object id)
        : base($"{entityName} с идентификатором '{id}' не найдено.")
    {
        Id = id;
        EntityName = entityName;
    }
}
