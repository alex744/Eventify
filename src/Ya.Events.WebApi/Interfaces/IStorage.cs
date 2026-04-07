namespace Ya.Events.WebApi.Interfaces;

public interface IStorage<T>
{
    List<T> Collection { get; }
}
