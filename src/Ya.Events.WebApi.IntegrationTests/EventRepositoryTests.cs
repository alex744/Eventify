using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Repositories;

namespace Ya.Events.WebApi.IntegrationTests;

public sealed class EventRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("testdb")
        .Build();

    private IServiceProvider _serviceProvider = null!;
    private IEventRepository _eventRepository = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        services.AddScoped<IEventRepository, EventRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _eventRepository = _serviceProvider.GetRequiredService<IEventRepository>();

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
    private async Task ResetDatabaseAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE events, bookings RESTART IDENTITY CASCADE");
    }

    #region CreateAsync Tests

    /// <summary>
    /// Проверяет, что событие успешно создаётся и возвращается с корректными данными.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task CreateAsync_WithValidEvent_ReturnsCreatedEvent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "Test Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 100
        );

        // Act
        var result = await _eventRepository.CreateAsync(@event, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(@event.Title, result.Title);
        Assert.Equal(@event.StartAt, result.StartAt);
        Assert.Equal(@event.EndAt, result.EndAt);
        Assert.Equal(@event.TotalSeats, result.TotalSeats);
        Assert.Equal(@event.TotalSeats, result.AvailableSeats);
    }

    /// <summary>
    /// Проверяет, что несколько событий могут быть созданы и все корректно сохранены в БД.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task CreateAsync_MultipleEvents_AllPersisted()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var events = Enumerable.Range(1, 5)
            .Select(i => new Event(
                title: $"Event {i}",
                startAt: futureDate.AddDays(i),
                endAt: futureDate.AddDays(i).AddHours(2),
                totalSeats: 50 * i
            ))
            .ToList();

        // Act
        var results = new List<Event>();
        foreach (var @event in events)
        {
            results.Add(await _eventRepository.CreateAsync(@event, CancellationToken.None));
        }

        // Assert
        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.NotEqual(Guid.Empty, r.Id));
        Assert.Equal(events.Select(e => e.Title).Order(), results.Select(r => r.Title).Order());
    }

    #endregion

    #region GetByIdAsync Tests

    /// <summary>
    /// Проверяет, что события могут быть получены по идентификатору и содержат корректные данные.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetByIdAsync_WithExistingId_ReturnsEvent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "Existing Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 50
        );
        var created = await _eventRepository.CreateAsync(@event, CancellationToken.None);

        // Act
        var result = await _eventRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.Title, result.Title);
    }

    /// <summary>
    /// Проверяет, что при запросе несуществующего события возвращается null.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _eventRepository.GetByIdAsync(nonExistentId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetAllAsync Tests

    /// <summary>
    /// Проверяет, что при пустой БД метод возвращает пустой результат с правильной метаинформацией.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithEmptyDatabase_ReturnsEmptyResult()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await _eventRepository.GetAllAsync(ct: CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
    }

    /// <summary>
    /// Проверяет, что все события возвращаются при запросе без фильтров.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithMultipleEvents_ReturnsAll()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        for (int i = 1; i <= 5; i++)
        {
            await _eventRepository.CreateAsync(new Event(
                title: $"Event {i}",
                startAt: futureDate.AddDays(i),
                endAt: futureDate.AddDays(i).AddHours(2),
                totalSeats: 50
            ), CancellationToken.None);
        }

        // Act
        var result = await _eventRepository.GetAllAsync(ct: CancellationToken.None);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что фильтр по названию возвращает только совпадающие события.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithTitleFilter_ReturnsMatchingEvents()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        await _eventRepository.CreateAsync(new Event(
            title: "Conference 2024",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 100
        ), CancellationToken.None);
        await _eventRepository.CreateAsync(new Event(
            title: "Workshop Basics",
            startAt: futureDate.AddDays(1),
            endAt: futureDate.AddDays(1).AddHours(2),
            totalSeats: 50
        ), CancellationToken.None);

        // Act
        var result = await _eventRepository.GetAllAsync(title: "Conference", ct: CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Conference 2024", result.Items.First().Title);
    }

    /// <summary>
    /// Проверяет, что фильтр по названию работает без учёта регистра букв.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithTitleFilter_CaseInsensitive()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        await _eventRepository.CreateAsync(new Event(
            title: "UPPERCASE EVENT",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 100
        ), CancellationToken.None);

        // Act
        var resultLower = await _eventRepository.GetAllAsync(title: "uppercase", ct: CancellationToken.None);
        var resultMixed = await _eventRepository.GetAllAsync(title: "Uppercase", ct: CancellationToken.None);

        // Assert
        Assert.Single(resultLower.Items);
        Assert.Single(resultMixed.Items);
    }

    /// <summary>
    /// Проверяет, что фильтр по дате возвращает только события в указанном диапазоне.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithDateRangeFilter_ReturnsEventsInRange()
    {
        // Arrange
        await ResetDatabaseAsync();
        var baseDate = DateTime.UtcNow.Date.AddDays(1);

        // Событие ДО диапазона
        await _eventRepository.CreateAsync(new Event(
            title: "Before",
            startAt: baseDate,
            endAt: baseDate.AddHours(2),
            totalSeats: 50
        ), CancellationToken.None);

        // Событие ВНУТРИ диапазона
        await _eventRepository.CreateAsync(new Event(
            title: "Inside",
            startAt: baseDate.AddDays(5),
            endAt: baseDate.AddDays(5).AddHours(2),
            totalSeats: 50
        ), CancellationToken.None);

        // Событие ПОСЛЕ диапазона
        await _eventRepository.CreateAsync(new Event(
            title: "After",
            startAt: baseDate.AddDays(10),
            endAt: baseDate.AddDays(10).AddHours(2),
            totalSeats: 50
        ), CancellationToken.None);

        // Act
        var from = baseDate.AddDays(2);
        var to = baseDate.AddDays(8);
        var result = await _eventRepository.GetAllAsync(
            from: from,
            to: to,
            ct: CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Inside", result.Items.First().Title);
    }

    /// <summary>
    /// Проверяет, что передача дат в неправильном порядке (from > to) выбрасывает исключение.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithInvalidDateRange_ThrowsException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var from = DateTime.UtcNow.AddDays(10);
        var to = DateTime.UtcNow.AddDays(1);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _eventRepository.GetAllAsync(from: from, to: to, ct: CancellationToken.None));
    }

    /// <summary>
    /// Проверяет, что пагинация работает корректно и возвращает правильное количество элементов на каждой странице.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        for (int i = 1; i <= 25; i++)
        {
            await _eventRepository.CreateAsync(new Event(
                title: $"Event {i:D2}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 2),
                totalSeats: 50
            ), CancellationToken.None);
        }

        // Act
        var page1 = await _eventRepository.GetAllAsync(page: 1, pageSize: 10, ct: CancellationToken.None);
        var page2 = await _eventRepository.GetAllAsync(page: 2, pageSize: 10, ct: CancellationToken.None);
        var page3 = await _eventRepository.GetAllAsync(page: 3, pageSize: 10, ct: CancellationToken.None);

        // Assert
        Assert.Equal(25, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.Equal(10, page2.Items.Count);
        Assert.Equal(5, page3.Items.Count);
    }

    /// <summary>
    /// Проверяет, что события возвращаются отсортированными по дате начала в убывающем порядке.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task GetAllAsync_SortedByDateDescending()
    {
        // Arrange
        await ResetDatabaseAsync();
        var baseDate = DateTime.UtcNow.AddDays(1);
        for (int i = 1; i <= 5; i++)
        {
            await _eventRepository.CreateAsync(new Event(
                title: $"Event {i}",
                startAt: baseDate.AddDays(i),
                endAt: baseDate.AddDays(i).AddHours(2),
                totalSeats: 50
            ), CancellationToken.None);
        }

        // Act
        var result = await _eventRepository.GetAllAsync(ct: CancellationToken.None);

        // Assert
        var dates = result.Items.Select(e => e.StartAt).ToList();
        Assert.Equal(dates.OrderByDescending(d => d), dates);
    }

    #endregion

    #region UpdateAsync Tests

    /// <summary>
    /// Проверяет, что событие может быть обновлено и все данные корректно сохранены.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task UpdateAsync_WithValidData_UpdatesEvent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "Original Title",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 50
        );
        var created = await _eventRepository.CreateAsync(@event, CancellationToken.None);

        var updated = new Event(
            title: "Updated Title",
            startAt: futureDate.AddDays(1),
            endAt: futureDate.AddDays(1).AddHours(3),
            totalSeats: 100
        );

        // Act
        var result = await _eventRepository.UpdateAsync(created.Id, updated, CancellationToken.None);

        // Assert
        Assert.Equal(created.Id, result.Id);
        Assert.Equal("Updated Title", result.Title);
        Assert.Equal(100, result.TotalSeats);
        Assert.Equal(futureDate.AddDays(1), result.StartAt);
    }

    /// <summary>
    /// Проверяет, что обновление несуществующего события выбрасывает NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task UpdateAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();
        var updated = new Event(
            title: "Updated",
            startAt: DateTime.UtcNow.AddDays(1),
            endAt: DateTime.UtcNow.AddDays(1).AddHours(2),
            totalSeats: 50
        );

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _eventRepository.UpdateAsync(nonExistentId, updated, CancellationToken.None));
    }

    /// <summary>
    /// Проверяет, что обновление события сохраняет его идентификатор.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task UpdateAsync_PreservesId_AndCreatedAtMetadata()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "Original",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 50
        );
        var created = await _eventRepository.CreateAsync(@event, CancellationToken.None);
        var originalId = created.Id;

        var updated = new Event(
            title: "Updated",
            startAt: futureDate.AddDays(1),
            endAt: futureDate.AddDays(1).AddHours(2),
            totalSeats: 75
        );

        // Act
        var result = await _eventRepository.UpdateAsync(originalId, updated, CancellationToken.None);

        // Assert
        Assert.Equal(originalId, result.Id);
    }

    #endregion

    #region DeleteAsync Tests

    /// <summary>
    /// Проверяет, что событие удаляется из БД и больше не может быть получено.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task DeleteAsync_WithExistingId_RemovesEvent()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "To Delete",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 50
        );
        var created = await _eventRepository.CreateAsync(@event, CancellationToken.None);

        // Act
        await _eventRepository.DeleteAsync(created.Id, CancellationToken.None);

        // Assert
        var result = await _eventRepository.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что удаление несуществующего события выбрасывает NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task DeleteAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        await ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _eventRepository.DeleteAsync(nonExistentId, CancellationToken.None));
    }

    #endregion

    #region SaveChangesAsync Tests

    /// <summary>
    /// Проверяет, что изменения в объектах событий корректно сохраняются в БД.
    /// </summary>
    [Fact]
    [Trait("Category", "EventRepository")]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        await ResetDatabaseAsync();
        var futureDate = DateTime.UtcNow.AddDays(1);
        var @event = new Event(
            title: "Test Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 50
        );
        var created = await _eventRepository.CreateAsync(@event, CancellationToken.None);

        // Act
        created.Title = "Modified Title";
        await _eventRepository.UpdateAsync(created.Id, created, CancellationToken.None);

        // Verify persistence by fetching fresh instance
        var result = await _eventRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Modified Title", result.Title);
    }

    #endregion
}
