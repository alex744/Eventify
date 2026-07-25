using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Ya.Bookings.Application.Abstractions.Persistence.Repositories;
using Ya.Bookings.Infrastructure.Persistence;
using Ya.Bookings.Infrastructure.Repositories;

namespace Ya.Bookings.IntegrationTests.Fixtures;

/// <summary>
/// Общая фикстура для всех интеграционных тестов.
/// Управляет единым экземпляром PostgreSQL контейнера для всех тестов в коллекции.
/// </summary>
public class PostgreSqlFixture : IAsyncLifetime
{
    private IServiceProvider _serviceProvider = null!;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("testdb")
        .Build();

    public IServiceProvider ServiceProvider
    {
        get => _serviceProvider;
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IBookingRepository, BookingRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Инициализируем БД с миграциями
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Подготавливает БД к новому тесту: очищает таблицы и сбрасывает identity.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE bookings RESTART IDENTITY CASCADE");
    }
}
