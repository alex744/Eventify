using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.Services;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.Exceptions;
using Ya.Events.Infrastructure.Persistence;
using Ya.Events.Infrastructure.Repositories;

namespace Ya.Events.Tests;

public sealed class EventServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IEventService _eventService;

    public EventServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IEventService, EventService>();

        var cacheMock = new Mock<ICache>();
        services.AddSingleton(cacheMock.Object);

        var cacheTtlMock = new Mock<ICacheTtlProvider>();
        cacheTtlMock.SetupGet(t => t.EventByIdTtl).Returns(TimeSpan.FromMinutes(5));
        cacheTtlMock.SetupGet(t => t.TopEventsTtl).Returns(TimeSpan.FromMinutes(1));
        services.AddSingleton(cacheTtlMock.Object);

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _eventService = _scope.ServiceProvider.GetRequiredService<IEventService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    #region CreateEventAsync Tests

    /// <summary>
    /// Проверяет, что создание события с корректными данными возвращает созданное событие.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateEventAsync_ValidEvent_ReturnsCreatedEvent()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        var expectedEvent = Event.Create(
            title: "Событие 1",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 10,
            description: "Описание 1");

        // Act
        var createdEvent = await _eventService.CreateAsync(expectedEvent, ct);

        // Assert
        Assert.NotNull(createdEvent);
        Assert.NotEqual(Guid.Empty, createdEvent.Id);
        Assert.Equal(expectedEvent.Title, createdEvent.Title);
        Assert.Equal(expectedEvent.StartAt, createdEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, createdEvent.EndAt);
        Assert.Equal(expectedEvent.TotalSeats, createdEvent.TotalSeats);
        Assert.Equal(expectedEvent.Description, createdEvent.Description);

        // Проверка, что GetById возвращает созданное событие
        var retrievedEvent = await _eventService.GetByIdAsync(createdEvent.Id, ct);
        Assert.NotNull(retrievedEvent);
        Assert.Equal(createdEvent.Id, retrievedEvent.Id);
    }

    /// <summary>
    /// Проверяет, что при создании события название обрезается от пробелов в начале и конце.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateEventAsync_WithTitleWhitespace_TrimsTitleAndCreatesEvent()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var createEvent = Event.Create(
            title: "  Test Event  ",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 10);

        // Act
        var result = await _eventService.CreateAsync(createEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Test Event", result.Title);
    }

    #endregion

    #region GetEventByIdAsync Tests

    /// <summary>
    /// Проверяет, что получение события по существующему идентификатору возвращает корректное событие.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetEventByIdAsync_ExistingId_ReturnsEvent()
    {
        // Arrange        
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        var expectedEvent = await _eventService.CreateAsync(Event.Create(
            title: "Событие 1",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 10,
            description: "Описание 1"), ct);
        var expectedId = expectedEvent.Id;

        // Act
        var result = await _eventService.GetByIdAsync(expectedId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(expectedEvent.Title, result.Title);
        Assert.Equal(expectedEvent.StartAt, result.StartAt);
        Assert.Equal(expectedEvent.EndAt, result.EndAt);
        Assert.Equal(expectedEvent.TotalSeats, result.TotalSeats);
        Assert.Equal(expectedEvent.Description, result.Description);
    }

    /// <summary>
    /// Проверяет, что попытка получить событие с несуществующим идентификатором возвращает null.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetEventByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange                
        var nonExistentId = Guid.NewGuid();

        // Act
        var notFoundEvent = await _eventService.GetByIdAsync(nonExistentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(notFoundEvent);
    }

    #endregion

    #region GetAllEventsAsync Tests

    /// <summary>
    /// Проверяет, что при отсутствии событий возвращается пустой массив и нулевое количество событий.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithNoEvents_ReturnsEmptyArray()
    {
        // Act & Assert
        var result = await _eventService.GetAllAsync(ct: TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    /// <summary>
    /// Проверяет, что получение всех событий возвращает полный список.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithMultipleEvents_ReturnsAllEvents()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        await _eventService.CreateAsync(Event.Create("Событие 3", futureDate.AddMonths(1), futureDate.AddMonths(1).AddHours(2), 30, "Описание 3"), ct);
        await _eventService.CreateAsync(Event.Create("Событие 2", futureDate.AddMonths(2), futureDate.AddMonths(2).AddHours(2), 20, "Описание 2"), ct);
        await _eventService.CreateAsync(Event.Create("Событие 1", futureDate.AddMonths(3), futureDate.AddMonths(3).AddHours(2), 10, "Описание 1"), ct);

        // Act
        var result = await _eventService.GetAllAsync(ct: ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal("Событие 1", result.Items[0].Title);
        Assert.Equal("Событие 2", result.Items[1].Title);
        Assert.Equal("Событие 3", result.Items[2].Title);
    }

    /// <summary>
    /// Проверяет, что фильтр по дате начала (from) корректно возвращает только события, начинающиеся после указанной даты.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithFromFilter_ReturnsFilteredEvents()
    {
        // Arrange        
        var futureDate1 = DateTime.UtcNow.AddDays(1);
        var futureDate2 = DateTime.UtcNow.AddDays(2);
        var filterDate = futureDate1.AddHours(1);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create("Event 1", futureDate1, futureDate1.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Event 2", futureDate2, futureDate2.AddHours(2), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(from: filterDate, ct: ct);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Event 2", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что фильтр по дате окончания (to) корректно возвращает только события, заканчивающиеся до указанной даты.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithToFilter_ReturnsFilteredEvents()
    {
        // Arrange        
        var futureDate1 = DateTime.UtcNow.AddDays(1);
        var futureDate2 = DateTime.UtcNow.AddDays(2);
        var filterDate = futureDate1.AddHours(3);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create("Event 1", futureDate1, futureDate1.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Event 2", futureDate2, futureDate2.AddHours(2), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(to: filterDate, ct: ct);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Event 1", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что фильтрация событий по названию возвращает только события с совпадающим заголовком.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_FilterByTitle_ReturnsMatchingEvents()
    {
        // Arrange                
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        await _eventService.CreateAsync(Event.Create("Conference 2024", futureDate, futureDate.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Meeting Q1", futureDate.AddHours(2), futureDate.AddHours(4), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(title: "Conference", ct: ct);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Conference 2024", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что фильтр по названию не учитывает регистр символов при поиске.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_FilterByTitle_IsCaseInsensitive()
    {
        // Arrange                
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        await _eventService.CreateAsync(Event.Create("Conference 2024", futureDate, futureDate.AddHours(2), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(title: "conference", ct: ct);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Conference 2024", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что фильтрация событий по диапазону дат возвращает события, попадающие в указанный интервал.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_FilterByDateRange_ReturnsEventsWithinRange()
    {
        // Arrange        
        var baseDate = DateTime.UtcNow.AddDays(1);
        var fromDate = baseDate;
        var toDate = baseDate.AddHours(4);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create("Событие 1", baseDate, baseDate.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Событие 2", baseDate.AddHours(2), baseDate.AddHours(4), 10), ct);
        await _eventService.CreateAsync(Event.Create("Событие 3", baseDate.AddHours(4), baseDate.AddHours(6), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(from: fromDate, to: toDate, ct: ct);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Equal("Событие 2", result.Items[0].Title);
        Assert.Equal("Событие 1", result.Items[1].Title);
    }

    /// <summary>
    /// Проверяет, что при корректном диапазоне дат (from меньше или равно to) метод GetAll не выбрасывает исключений
    /// и возвращает результат (даже если он пуст).
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WhenFromLessThanOrEqualToTo_DoesNotThrow()
    {
        // Arrange        
        var baseDate = DateTime.UtcNow.AddDays(1);
        var from = baseDate;
        var to = baseDate.AddDays(3);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create("Событие 1", baseDate, baseDate.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Событие 2", baseDate.AddDays(1), baseDate.AddDays(1).AddHours(2), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(from: from, to: to, ct: ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// Проверяет, что при применении нескольких фильтров одновременно (дата начала, дата окончания и название) возвращаются только события, соответствующие всем критериям.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithMultipleFilters_ReturnsFilteredEvents()
    {
        // Arrange     
        var baseDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        await _eventService.CreateAsync(Event.Create("Conference 2024", baseDate, baseDate.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Conference 2025", baseDate.AddDays(5), baseDate.AddDays(5).AddHours(2), 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(
            from: baseDate.AddDays(2),
            to: baseDate.AddDays(6),
            title: "Conference",
            ct: ct);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("Conference 2025", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что при передаче значения from больше, чем to, метод GetAll выбрасывает исключение ArgumentException
    /// с корректным сообщением об ошибке.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetAllEventsAsync_WhenFromGreaterThanTo_ThrowsArgumentException()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.AddDays(1);
        var from = baseDate.AddDays(6);
        var to = baseDate.AddDays(2);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create("Событие 1", baseDate, baseDate.AddHours(2), 10), ct);
        await _eventService.CreateAsync(Event.Create("Событие 2", baseDate.AddDays(5), baseDate.AddDays(5).AddHours(2), 10), ct);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _eventService.GetAllAsync(from: from, to: to, ct: ct));
        Assert.Equal("Дата начала (from) не может быть позже даты окончания (to).", exception.Message);
    }

    #endregion

    #region Pagination Tests

    /// <summary>
    /// Проверяет, что по умолчанию возвращается первая страница с размером страницы 10 элементов.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithDefaultPagination_ReturnsFirstPageWithDefaultPageSize()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 15; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(ct: ct);

        // Assert
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(10, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что при указании пользовательского размера страницы возвращается корректное количество элементов на странице.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithCustomPageSize_ReturnsCorrectNumberOfItems()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 25; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(page: 1, pageSize: 5, ct: ct);

        // Assert
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(1, result.CurrentPage);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что вторая страница содержит правильные элементы согласно переданным параметрам пагинации.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithSecondPage_ReturnsCorrectItems()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 25; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(page: 2, pageSize: 10, ct: ct);

        // Assert
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(10, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что последняя страница корректно возвращает оставшиеся элементы, которые не заполняют полный размер страницы.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithLastPagePartialResults_ReturnsRemainingItems()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 23; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(page: 3, pageSize: 10, ct: ct);

        // Assert
        Assert.Equal(23, result.TotalCount);
        Assert.Equal(3, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(3, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что при запросе страницы за границей общего количества страниц возвращается пустой массив элементов.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithPageBeyondTotal_ReturnsEmptyItems()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 5; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(page: 10, pageSize: 10, ct: ct);

        // Assert
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(10, result.CurrentPage);
        Assert.Equal(10, result.PageSize);
        Assert.Empty(result.Items);
    }

    /// <summary>
    /// Проверяет, что пагинация возвращает корректную страницу событий.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange: создаём 15 событий с последовательными датами
        var baseDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        for (int i = 1; i <= 15; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                 title: $"Событие {i}",
                 startAt: baseDate.AddDays(i),
                 endAt: baseDate.AddDays(i).AddHours(2),
                 totalSeats: i * 10,
                 description: $"Описание {i}"), ct);
        }

        // Act: запрашиваем вторую страницу, размер страницы 5
        int page = 2;
        int pageSize = 5;
        var result = await _eventService.GetAllAsync(page: page, pageSize: pageSize, ct: ct);

        // Assert: проверяем метаданные пагинации
        Assert.NotNull(result);
        Assert.Equal(15, result.TotalCount);
        Assert.Equal(page, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(pageSize, result.Items.Count);

        // Ожидаемые элементы: сортировка по StartAt descending, 
        // на второй странице должны быть элементы с 6 по 10 (в порядке убывания дат)
        var expectedIds = (await _eventService.GetAllAsync(ct: ct)).Items
            .OrderByDescending(e => e.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => e.Id)
            .ToList();

        var actualIds = result.Items.Select(e => e.Id).ToList();
        Assert.Equal(expectedIds, actualIds);
    }

    /// <summary>
    /// Проверяет, что пагинация и фильтры работают вместе (название + пагинация), возвращая правильное подмножество отфильтрованных результатов.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithPaginationAndFilters_ReturnsPaginatedFilteredResults()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 30; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Conference {i}",
                startAt: baseDate.AddDays(i),
                endAt: baseDate.AddDays(i).AddHours(2),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(
            page: 2,
            pageSize: 5,
            title: "Conference",
            ct: ct);

        // Assert
        Assert.Equal(30, result.TotalCount);
        Assert.Equal(2, result.CurrentPage);
        Assert.Equal(5, result.PageSize);
        Assert.Equal(5, result.Items.Count);
    }

    /// <summary>
    /// Проверяет, что комбинированное применение фильтров (название + даты + пагинация) возвращает корректный результат.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_CombinedFilters_ReturnsFilteredEvents()
    {
        // Arrange
        var baseDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        await _eventService.CreateAsync(Event.Create("Конференция по маркетингу", baseDate.AddDays(14), baseDate.AddDays(19), 100, "Описание"), ct);
        await _eventService.CreateAsync(Event.Create("Конференция по дизайну", baseDate.AddDays(13), baseDate.AddDays(14), 50, "Описание"), ct);
        await _eventService.CreateAsync(Event.Create("Встреча разработчиков", baseDate.AddDays(10), baseDate.AddDays(12), 200, "Описание"), ct);
        await _eventService.CreateAsync(Event.Create("Конференция по IT", baseDate.AddDays(9), baseDate.AddDays(11), 80, "Описание"), ct);
        await _eventService.CreateAsync(Event.Create("Конференция по бизнесу", baseDate.AddDays(8), baseDate.AddDays(9), 120, "Описание"), ct);

        // Фильтры
        var titleFilter = "конференция";
        var fromDate = baseDate.AddDays(8);
        var toDate = baseDate.AddDays(15);
        int page = 1;
        int pageSize = 2;

        // Act
        var result = await _eventService.GetAllAsync(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize, ct: ct);

        // Assert - общее количество отфильтрованных событий (3)
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(page, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(2, result.Items.Count); // на первой странице 2 элемента

        // Ожидаемый порядок: сортировка по StartAt DESC (бизнес (9-10), IT (10-12), дизайн (14-15))
        // Первая страница должна содержать бизнес и IT
        var expectedFirstPage = new[] { "Конференция по дизайну", "Конференция по IT" };
        var actualTitles = result.Items.Select(e => e.Title).ToArray();
        Assert.Equal(expectedFirstPage, actualTitles);

        // Act - вторая страница
        page = 2;
        pageSize = 1;
        result = await _eventService.GetAllAsync(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize, ct: ct);

        // Assert - вторая страница содержит только оставшееся событие
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(page, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("Конференция по IT", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что размер страницы в 1 элемент корректно возвращает по одному элементу на каждой странице.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_WithPaginationPageSizeOne_ReturnsOneItemPerPage()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 3; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        var result = await _eventService.GetAllAsync(page: 2, pageSize: 1, ct: ct);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.CurrentPage);
        Assert.Equal(1, result.PageSize);
        Assert.Single(result.Items);
    }

    /// <summary>
    /// Проверяет, что вычисление общего количества страниц корректно для нецелого деления количества элементов на размер страницы.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllEventsAsync_TotalPagesCalculation_IsCorrect()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        for (int i = 1; i <= 37; i++)
        {
            await _eventService.CreateAsync(Event.Create(
                title: $"Event {i}",
                startAt: futureDate.AddHours(i),
                endAt: futureDate.AddHours(i + 1),
                totalSeats: 10), ct);
        }

        // Act
        var result = await _eventService.GetAllAsync(pageSize: 10, ct: ct);

        // Assert: TotalPages
        Assert.Equal(4, (result.TotalCount + result.PageSize - 1) / result.PageSize);
    }

    /// <summary>
    /// Проверяет, что нумерация страниц начинается с 1, а не с 0.
    /// </summary>
    [Fact]
    public async Task GetAllEventsAsync_FirstPageIsOne_NotZero()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        await _eventService.CreateAsync(Event.Create(
            title: "Event 1",
            startAt: futureDate,
            endAt: futureDate.AddHours(1),
            totalSeats: 10), ct);

        // Act
        var result = await _eventService.GetAllAsync(page: 1, pageSize: 10, ct: ct);

        // Assert
        Assert.Equal(1, result.CurrentPage);
    }

    #endregion

    #region UpdateEventAsync Tests

    /// <summary>
    /// Проверяет, что обновление существующего события выполняется успешно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task UpdateEventAsync_ExistingEvent_UpdatesSuccessfully()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;

        var createdEvent = await _eventService.CreateAsync(Event.Create(
            title: "Событие 1",
            startAt: futureDate.AddDays(1),
            endAt: futureDate.AddDays(2),
            totalSeats: 10,
            description: "Описание 1"), ct);

        var expectedEvent = Event.Create(
            title: "Обновлённое событие",
            startAt: futureDate.AddDays(2),
            endAt: futureDate.AddDays(3),
            totalSeats: 15,
            description: "Новое описание");

        // Act
        var updatedEvent = await _eventService.UpdateAsync(createdEvent.Id, expectedEvent, ct);

        // Assert
        Assert.NotNull(updatedEvent);
        Assert.Equal(createdEvent.Id, updatedEvent.Id);
        Assert.Equal(expectedEvent.Title, updatedEvent.Title);
        Assert.Equal(expectedEvent.StartAt, updatedEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, updatedEvent.EndAt);
        Assert.Equal(expectedEvent.Description, updatedEvent.Description);
    }

    /// <summary>
    /// Проверяет, что попытка обновить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task UpdateEventAsync_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var invalidId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var expectedEvent = Event.Create(
            title: "Обновлённое название",
            startAt: DateTime.UtcNow.AddDays(1),
            endAt: DateTime.UtcNow.AddDays(2),
            totalSeats: 5,
            description: "Новое описание");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _eventService.UpdateAsync(invalidId, expectedEvent, ct));
        Assert.Equal($"Событие с идентификатором '{invalidId}' не найдено.", exception.Message);
    }

    #endregion

    #region DeleteEventAsync Tests

    /// <summary>
    /// Проверяет, что удаление существующего события выполняется успешно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task DeleteEventAsync_ExistingEvent_DeletesSuccessfully()
    {
        // Arrange       
        var futureDate = DateTime.UtcNow.AddDays(1);
        var ct = TestContext.Current.CancellationToken;
        var createdEvent = await _eventService.CreateAsync(Event.Create("Событие 1", futureDate, futureDate.AddDays(1), 10), ct);

        // Act
        await _eventService.DeleteAsync(createdEvent.Id, ct);

        // Assert        
        var deletedEvent = await _eventService.GetByIdAsync(createdEvent.Id, ct);
        Assert.Null(deletedEvent);
    }

    /// <summary>
    /// Проверяет, что попытка удалить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task DeleteEventAsync_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _eventService.DeleteAsync(nonExistentId, ct));
        Assert.Equal($"Событие с идентификатором '{nonExistentId}' не найдено.", exception.Message);
    }

    #endregion    
}
