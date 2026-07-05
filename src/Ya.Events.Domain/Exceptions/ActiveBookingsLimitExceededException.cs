namespace Ya.Events.Domain.Exceptions;

public class ActiveBookingsLimitExceededException : Exception
{
    public int CurrentActiveBookings { get; }
    public int MaxAllowedBookings { get; }

    public ActiveBookingsLimitExceededException()
        : base("Превышен лимит активных броней.")
    {
    }

    public ActiveBookingsLimitExceededException(int currentActiveBookings, int maxAllowedBookings)
        : base($"Превышен лимит активных броней. Текущих: {currentActiveBookings}, максимум: {maxAllowedBookings}")
    {
        CurrentActiveBookings = currentActiveBookings;
        MaxAllowedBookings = maxAllowedBookings;
    }

    public ActiveBookingsLimitExceededException(string message)
        : base(message)
    {
    }

    public ActiveBookingsLimitExceededException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
