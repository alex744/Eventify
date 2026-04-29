using Moq;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Services;

namespace Ya.Events.WebApi.Tests;

public class EventServiceTests
{
    private readonly List<Event> _events;
    private readonly Mock<IStore<Event>> _mockStore;
    private readonly EventService _service;

    public EventServiceTests()
    {
        _events = new List<Event>
        {
            new("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2),10, "Описание 1"),
            new("Событие 2", new DateTime(2026, 1, 3), new DateTime(2026, 1, 4),10, "Описание 2")
        };
        _mockStore = new Mock<IStore<Event>>();
        _mockStore.Setup(s => s.Collection).Returns(_events);
        _service = new EventService(_mockStore.Object);
    }

    /// <summary>
    /// Проверяет, что создание события с корректными данными возвращает созданное событие.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task Create_ValidEvent_ReturnsCreatedEvent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var expectedEvent = new Event(
            title: "Событие 1",
            startAt: new DateTime(2026, 1, 1),
            endAt: new DateTime(2026, 1, 2),
            totalSeats: 10,
            description: "Описание 1");

        // Act
        var createdEvent = await _service.CreateAsync(expectedEvent, ct);

        // Assert
        Assert.NotNull(createdEvent);
        Assert.NotEqual(Guid.Empty, createdEvent.Id);
        Assert.Equal(expectedEvent.Title, createdEvent.Title);
        Assert.Equal(expectedEvent.StartAt, createdEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, createdEvent.EndAt);
        Assert.Equal(expectedEvent.TotalSeats, createdEvent.TotalSeats);
        Assert.Equal(expectedEvent.Description, createdEvent.Description);

        // Проверка, что событие добавлено в хранилище
        Assert.Contains(createdEvent, _events);

        // Проверка, что GetById возвращает созданное событие
        var retrievedEvent = await _service.GetByIdAsync(createdEvent.Id, ct);
        Assert.NotNull(retrievedEvent);
        Assert.Equal(createdEvent.Id, retrievedEvent.Id);
    }

    /// <summary>
    /// Проверяет, что получение всех событий возвращает полный список.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_ReturnsAllEvents()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var events = new List<Event>
        {
            new("Событие 3", new DateTime(2026, 1, 5), new DateTime(2026, 1, 6),30, "Описание 3"),
            new("Событие 2", new DateTime(2026, 1, 3), new DateTime(2026, 1, 4),20, "Описание 2"),
            new("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2),10, "Описание 1")
        };

        var mockStorage = new Mock<IStore<Event>>();
        mockStorage.Setup(s => s.Collection).Returns(events);
        var service = new EventService(mockStorage.Object);

        // Act
        var result = await service.GetAllAsync(ct: ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(events[0].Title, result.Items[0].Title);
        Assert.Equal(events[1].Title, result.Items[1].Title);
        Assert.Equal(events[2].Title, result.Items[2].Title);
    }

    /// <summary>
    /// Проверяет, что получение события по существующему идентификатору возвращает корректное событие.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetById_ExistingId_ReturnsEvent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var existingEvent = _events[0];
        var expectedId = existingEvent.Id;

        // Act
        var result = await _service.GetByIdAsync(expectedId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(existingEvent.Title, result.Title);
        Assert.Equal(existingEvent.StartAt, result.StartAt);
        Assert.Equal(existingEvent.EndAt, result.EndAt);
        Assert.Equal(existingEvent.TotalSeats, result.TotalSeats);
        Assert.Equal(existingEvent.Description, result.Description);
    }

    /// <summary>
    /// Проверяет, что обновление существующего события выполняется успешно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task Update_ExistingEvent_UpdatesSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var existingEvent = _events[0];
        var id = existingEvent.Id;
        var expectedEvent = new Event(
            title: "Обновлённое событие",
            startAt: new DateTime(2026, 2, 1),
            endAt: new DateTime(2026, 2, 2),
            totalSeats: 15,
            description: "Новое описание");

        // Act
        var updatedEvent = await _service.UpdateAsync(id, expectedEvent, ct);

        // Assert
        Assert.NotNull(updatedEvent);
        Assert.Equal(id, updatedEvent.Id);
        Assert.Equal(expectedEvent.Title, updatedEvent.Title);
        Assert.Equal(expectedEvent.StartAt, updatedEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, updatedEvent.EndAt);
        Assert.Equal(expectedEvent.Description, updatedEvent.Description);
    }

    /// <summary>
    /// Проверяет, что удаление существующего события выполняется успешно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task Delete_ExistingEvent_DeletesSuccessfully()
    {
        // Arrange       
        var ct = TestContext.Current.CancellationToken;
        var id = _events[0].Id;

        // Act
        await _service.DeleteAsync(id, ct);

        // Assert        
        Assert.DoesNotContain(_events, e => e.Id == id);
        Assert.Single(_events);
    }

    /// <summary>
    /// Проверяет, что фильтрация событий по названию возвращает только события с совпадающим заголовком.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_FilterByTitle_ReturnsMatchingEvents()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var search = "событие";

        // Act
        var result = await _service.GetAllAsync(title: search, ct: ct);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, e => e.Title.Contains("1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Items, e => e.Title.Contains("2", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Проверяет, что фильтрация событий по диапазону дат возвращает события, попадающие в указанный интервал.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_FilterByDateRange_ReturnsEventsWithinRange()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 1, 4);

        // Act
        var result = await _service.GetAllAsync(from: fromDate, to: toDate, ct: ct);

        // Assert
        Assert.Equal(2, result.TotalCount);
        Assert.Contains(result.Items, e => e.Title.Contains("1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Items, e => e.Title.Contains("2", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Проверяет, что пагинация возвращает корректную страницу событий.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_WithPagination_ReturnsCorrectPage()
    {
        // Arrange: создаём 15 событий с последовательными датами
        var ct = TestContext.Current.CancellationToken;
        var events = new List<Event>();
        for (int i = 1; i <= 15; i++)
        {
            events.Add(new Event(
                title: $"Событие {i}",
                startAt: new DateTime(2026, 1, i),
                endAt: new DateTime(2026, 1, i + 1),
                totalSeats: i * 10,
                description: $"Описание {i}"
            ));
        }

        var mockStorage = new Mock<IStore<Event>>();
        mockStorage.Setup(s => s.Collection).Returns(events);
        var service = new EventService(mockStorage.Object);

        // Act: запрашиваем вторую страницу, размер страницы 5
        int page = 2;
        int pageSize = 5;
        var result = await service.GetAllAsync(page: page, pageSize: pageSize, ct: ct);

        // Assert: проверяем метаданные пагинации
        Assert.NotNull(result);
        Assert.Equal(events.Count, result.TotalCount);
        Assert.Equal(page, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(pageSize, result.Items.Count);

        // Ожидаемые элементы: сортировка по StartAt descending, 
        // на второй странице должны быть элементы с 6 по 10 (в порядке убывания дат)
        var expectedIds = events
            .OrderByDescending(e => e.StartAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => e.Id)
            .ToList();

        var actualIds = result.Items.Select(e => e.Id).ToList();
        Assert.Equal(expectedIds, actualIds);
    }

    /// <summary>
    /// Проверяет, что комбинированное применение фильтров (название + даты + пагинация) возвращает корректный результат.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_CombinedFilters_ReturnsFilteredEvents()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var events = new List<Event>
        {
            new Event("Конференция по маркетингу", new DateTime(2026, 1, 15), new DateTime(2026, 1, 20),100, "Описание"),
            new Event("Конференция по дизайну", new DateTime(2026, 1, 14), new DateTime(2026, 1, 15),50, "Описание"),
            new Event("Встреча разработчиков", new DateTime(2026, 1, 11), new DateTime(2026, 1, 13),200, "Описание"),
            new Event("Конференция по IT", new DateTime(2026, 1, 10), new DateTime(2026, 1, 12),80, "Описание"),
            new Event("Конференция по бизнесу", new DateTime(2026, 1, 9), new DateTime(2026, 1, 10),120, "Описание")
        };

        var mockStorage = new Mock<IStore<Event>>();
        mockStorage.Setup(s => s.Collection).Returns(events);
        var service = new EventService(mockStorage.Object);

        // Фильтры
        var titleFilter = "конференция";
        var fromDate = new DateTime(2026, 1, 9);
        var toDate = new DateTime(2026, 1, 15);
        int page = 1;
        int pageSize = 2;

        // Act
        var result = await service.GetAllAsync(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize, ct: ct);

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
        result = await service.GetAllAsync(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize, ct: ct);

        // Assert - вторая страница содержит только оставшееся событие
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(page, result.CurrentPage);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Single(result.Items);
        Assert.Equal("Конференция по IT", result.Items[0].Title);
    }

    /// <summary>
    /// Проверяет, что при корректном диапазоне дат (from меньше или равно to) метод GetAll не выбрасывает исключений
    /// и возвращает результат (даже если он пуст).
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAll_WhenFromLessThanOrEqualToTo_DoesNotThrow()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 12, 31);

        // Act
        var result = await _service.GetAllAsync(from: from, to: to, ct: ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
    }

    /// <summary>
    /// Проверяет, что при передаче значения from больше, чем to, метод GetAll выбрасывает исключение ArgumentException
    /// с корректным сообщением об ошибке.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetAll_WhenFromGreaterThanTo_ThrowsArgumentException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var from = new DateTime(2025, 12, 31);
        var to = new DateTime(2025, 1, 1);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => _service.GetAllAsync(from: from, to: to, ct: ct));
        Assert.Equal("Дата начала (from) не может быть позже даты окончания (to).", exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка получить событие с несуществующим идентификатором возвращает null.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetById_NonExistentId_ReturnsNull()
    {
        // Arrange        
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();

        // Act
        var notFoundEvent = await _service.GetByIdAsync(nonExistentId, ct);

        // Assert
        Assert.Null(notFoundEvent);
    }

    /// <summary>
    /// Проверяет, что попытка обновить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task Update_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();
        var expectedEvent = new Event(
            title: "Обновлённое название",
            startAt: new DateTime(2026, 2, 1),
            endAt: new DateTime(2026, 2, 2),
            totalSeats: 5,
            description: "Новое описание");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(nonExistentId, expectedEvent, ct));
        Assert.Equal($"Событие с идентификатором '{nonExistentId}' не найдено.", exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка удалить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task Delete_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _service.DeleteAsync(nonExistentId, ct));
        Assert.Equal($"Событие с идентификатором '{nonExistentId}' не найдено.", exception.Message);
    }
}
