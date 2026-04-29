namespace Ya.Events.WebApi.Exceptions
{
    public class NoAvailableSeatsException : Exception
    {
        public NoAvailableSeatsException() { }
        public NoAvailableSeatsException(string message) : base(message) { }
        public NoAvailableSeatsException(string message, Exception inner) : base(message, inner) { }
    }
}
