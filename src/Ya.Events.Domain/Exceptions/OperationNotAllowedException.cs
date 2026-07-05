namespace Ya.Events.Domain.Exceptions;

public class OperationNotAllowedException : Exception
{
    public OperationNotAllowedException()
        : base("Операция не разрешена.")
    {
    }

    public OperationNotAllowedException(string message)
        : base(message)
    {
    }

    public OperationNotAllowedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
