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
            new("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), "Описание 1"),
            new("Событие 2", new DateTime(2026, 1, 3), new DateTime(2026, 1, 4), "Описание 2")
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
    public void Create_ValidEvent_ReturnsCreatedEvent()
    {
        // Arrange
        var expectedEvent = new Event(
            title: "Событие 1",
            startAt: new DateTime(2026, 1, 1),
            endAt: new DateTime(2026, 1, 2),
            description: "Описание 1");

        // Act
        var createdEvent = _service.Create(expectedEvent);

        // Assert
        Assert.NotNull(createdEvent);
        Assert.NotEqual(Guid.Empty, createdEvent.Id);
        Assert.Equal(expectedEvent.Title, createdEvent.Title);
        Assert.Equal(expectedEvent.StartAt, createdEvent.StartAt);
        Assert.Equal(expectedEvent.EndAt, createdEvent.EndAt);
        Assert.Equal(expectedEvent.Description, createdEvent.Description);

        // Проверка, что событие добавлено в хранилище
        Assert.Contains(createdEvent, _events);

        // Проверка, что GetById возвращает созданное событие
        var retrievedEvent = _service.GetById(createdEvent.Id);
        Assert.NotNull(retrievedEvent);
        Assert.Equal(createdEvent.Id, retrievedEvent.Id);
    }

    /// <summary>
    /// Проверяет, что получение всех событий возвращает полный список.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void GetAll_ReturnsAllEvents()
    {
        // Arrange
        var events = new List<Event>
        {
            new("Событие 3", new DateTime(2026, 1, 5), new DateTime(2026, 1, 6), "Описание 3"),
            new("Событие 2", new DateTime(2026, 1, 3), new DateTime(2026, 1, 4), "Описание 2"),
            new("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), "Описание 1")
        };

        var mockStorage = new Mock<IStore<Event>>();
        mockStorage.Setup(s => s.Collection).Returns(events);
        var service = new EventService(mockStorage.Object);

        // Act
        var result = service.GetAll().Items;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(events.Count, result.Count);
        Assert.Equal(events[0].Title, result[0].Title);
        Assert.Equal(events[1].Title, result[1].Title);
        Assert.Equal(events[2].Title, result[2].Title);
    }

    /// <summary>
    /// Проверяет, что получение события по существующему идентификатору возвращает корректное событие.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void GetById_ExistingId_ReturnsEvent()
    {
        // Arrange
        var existingEvent = _events[0];
        var expectedId = existingEvent.Id;

        // Act
        var result = _service.GetById(expectedId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
        Assert.Equal(existingEvent.Title, result.Title);
        Assert.Equal(existingEvent.StartAt, result.StartAt);
        Assert.Equal(existingEvent.EndAt, result.EndAt);
        Assert.Equal(existingEvent.Description, result.Description);
    }

    /// <summary>
    /// Проверяет, что обновление существующего события выполняется успешно.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void Update_ExistingEvent_UpdatesSuccessfully()
    {
        // Arrange
        var existingEvent = _events[0];
        var id = existingEvent.Id;
        var expectedEvent = new Event(
            title: "Обновлённое событие",
            description: "Новое описание",
            startAt: new DateTime(2026, 2, 1),
            endAt: new DateTime(2026, 2, 2));

        // Act
        var updatedEvent = _service.Update(id, expectedEvent);

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
    public void Delete_ExistingEvent_DeletesSuccessfully()
    {
        // Arrange       
        var id = _events[0].Id;

        // Act
        _service.Delete(id);

        // Assert        
        Assert.DoesNotContain(_events, e => e.Id == id);
        Assert.Single(_events);
    }

    /// <summary>
    /// Проверяет, что фильтрация событий по названию возвращает только события с совпадающим заголовком.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void GetAll_FilterByTitle_ReturnsMatchingEvents()
    {
        // Arrange
        var search = "событие";

        // Act
        var result = _service.GetAll(search);

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
    public void GetAll_FilterByDateRange_ReturnsEventsWithinRange()
    {
        // Arrange
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 1, 4);

        // Act
        var result = _service.GetAll(from: fromDate, to: toDate);

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
    public void GetAll_WithPagination_ReturnsCorrectPage()
    {
        // Arrange: создаём 15 событий с последовательными датами
        var events = new List<Event>();
        for (int i = 1; i <= 15; i++)
        {
            events.Add(new Event(
                title: $"Событие {i}",
                startAt: new DateTime(2026, 1, i),
                endAt: new DateTime(2026, 1, i + 1),
                description: $"Описание {i}"
            ));
        }

        var mockStorage = new Mock<IStore<Event>>();
        mockStorage.Setup(s => s.Collection).Returns(events);
        var service = new EventService(mockStorage.Object);

        // Act: запрашиваем вторую страницу, размер страницы 5
        int page = 2;
        int pageSize = 5;
        var result = service.GetAll(page: page, pageSize: pageSize);

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
    public void GetAll_CombinedFilters_ReturnsFilteredEvents()
    {
        // Arrange
        var events = new List<Event>
        {
            new Event("Конференция по маркетингу", new DateTime(2026, 1, 15), new DateTime(2026, 1, 20), "Описание"),
            new Event("Конференция по дизайну", new DateTime(2026, 1, 14), new DateTime(2026, 1, 15), "Описание"),
            new Event("Встреча разработчиков", new DateTime(2026, 1, 11), new DateTime(2026, 1, 13), "Описание"),
            new Event("Конференция по IT", new DateTime(2026, 1, 10), new DateTime(2026, 1, 12), "Описание"),
            new Event("Конференция по бизнесу", new DateTime(2026, 1, 9), new DateTime(2026, 1, 10), "Описание")
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
        var result = service.GetAll(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize);

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
        result = service.GetAll(title: titleFilter, from: fromDate, to: toDate, page: page, pageSize: pageSize);

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
    public void GetAll_WhenFromLessThanOrEqualToTo_DoesNotThrow()
    {
        // Arrange        
        var from = new DateTime(2026, 1, 1);
        var to = new DateTime(2026, 12, 31);

        // Act
        var result = _service.GetAll(from: from, to: to);

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
    public void GetAll_WhenFromGreaterThanTo_ThrowsArgumentException()
    {
        // Arrange        
        var from = new DateTime(2025, 12, 31);
        var to = new DateTime(2025, 1, 1);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.GetAll(from: from, to: to));
        Assert.Equal("Дата начала (from) не может быть позже даты окончания (to).", exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка получить событие с несуществующим идентификатором возвращает null.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void GetById_NonExistentId_ReturnsNull()
    {
        // Arrange        
        var nonExistentId = Guid.NewGuid();

        // Act
        var notFoundEvent = _service.GetById(nonExistentId);

        // Assert
        Assert.Null(notFoundEvent);
    }

    /// <summary>
    /// Проверяет, что попытка обновить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Update_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var expectedEvent = new Event(
            title: "Обновлённое название",
            startAt: new DateTime(2026, 2, 1),
            endAt: new DateTime(2026, 2, 2),
            description: "Новое описание");

        // Act & Assert
        var exception = Assert.Throws<NotFoundException>(() => _service.Update(nonExistentId, expectedEvent));
        Assert.Equal($"Событие с идентификатором '{nonExistentId}' не найдено.", exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка удалить событие с несуществующим идентификатором вызывает исключение NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public void Delete_NonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act & Assert
        var exception = Assert.Throws<NotFoundException>(() => _service.Delete(nonExistentId));
        Assert.Equal($"Событие с идентификатором '{nonExistentId}' не найдено.", exception.Message);
    }
}
