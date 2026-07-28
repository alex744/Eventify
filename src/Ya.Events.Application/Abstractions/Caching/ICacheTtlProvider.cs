namespace Ya.Events.Application.Abstractions.Caching;

public interface ICacheTtlProvider
{
    TimeSpan EventByIdTtl { get; }
    TimeSpan TopEventsTtl { get; }
}