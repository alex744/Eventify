using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Exceptions;

public class BookingNotPendingException : Exception
{
    public Booking? Booking { get; }

    public BookingNotPendingException()
        : base("Бронь не находится в статусе Pending")
    {
    }

    public BookingNotPendingException(Booking booking)
        : base($"Бронь '{booking.Id}' не в статусе 'Pending' (текущий статус: '{booking.Status}')")
    {
        Booking = booking;
    }

    public BookingNotPendingException(Booking booking, string message)
        : base(message)
    {
        Booking = booking;
    }

    public BookingNotPendingException(Booking booking, string message, Exception inner)
        : base(message, inner)
    {
        Booking = booking;
    }
}
