namespace Ya.Bookings.Domain.Exceptions;

public class TooManyActiveBookingsException : Exception
{
    public TooManyActiveBookingsException() { }
    public TooManyActiveBookingsException(string message) : base(message) { }
    public TooManyActiveBookingsException(string message, Exception inner) : base(message, inner) { }
}
