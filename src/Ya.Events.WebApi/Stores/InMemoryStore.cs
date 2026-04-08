using Ya.Events.WebApi.Interfaces;

namespace Ya.Events.WebApi.Stores;

public class InMemoryStore<T> : IStore<T>
{
    private readonly List<T> _collection;

    public InMemoryStore(List<T> collection)
    {
        _collection = collection;
    }

    public List<T> Collection => _collection;
}
