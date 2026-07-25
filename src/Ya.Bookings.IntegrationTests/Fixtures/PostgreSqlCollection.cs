namespace Ya.Bookings.IntegrationTests.Fixtures;

/// <summary>
/// Определяет коллекцию тестов, которые будут использовать общую PostgreSQL фикстуру.
/// </summary>
[CollectionDefinition("PostgreSQL collection")]
public class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
{
    // Этот класс не содержит тестов, он используется только для определения коллекции фикстур
}
