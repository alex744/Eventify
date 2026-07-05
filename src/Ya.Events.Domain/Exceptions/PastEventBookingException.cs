namespace Ya.Events.Domain.Exceptions;

public class PastEventBookingException : Exception
{
    public PastEventBookingException()
        : base("Невозможно зарезервировать место на прошедшем событии.")
    {
    }

    public PastEventBookingException(string message)
        : base(message)
    {
    }

    public PastEventBookingException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
