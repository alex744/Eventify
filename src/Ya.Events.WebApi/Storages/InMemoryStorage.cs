using Ya.Events.WebApi.Interfaces;

namespace Ya.Events.WebApi.Storages;

public class InMemoryStorage<T> : IStorage<T>
{
    private readonly List<T> _collection;

    public InMemoryStorage(List<T> collection)
    {
        _collection = collection;
    }

    public List<T> Collection => _collection;
}
