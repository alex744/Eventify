namespace Ya.Events.WebApi.Interfaces;

public interface IStore<T>
{
    List<T> Collection { get; }
}
