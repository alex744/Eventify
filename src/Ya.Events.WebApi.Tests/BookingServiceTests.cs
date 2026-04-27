using Moq;
using System.Collections.Concurrent;
using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Services;

namespace Ya.Events.WebApi.Tests;

public class BookingServiceTests
{
    private readonly Mock<IBookingStore> _bookingStoreMock = new();
    private readonly Mock<IEventService> _eventServiceMock = new();
    private readonly BookingService _bookingService;

    public BookingServiceTests()
    {
        _bookingService = new BookingService(_bookingStoreMock.Object, _eventServiceMock.Object);
    }

    /// <summary>
    /// Создание брони для существующего события возвращает бронь в статусе Pending и уменьшает AvailableSeats на 1
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WhenEventExists_ReturnsBookingWithPendingStatusAndDecreasesSeats()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var existingEvent = new Event("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), 10, "Описание 1");
        var initialSeats = existingEvent.AvailableSeats;
        var eventId = existingEvent.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        Booking? capturedBooking = null;
        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((b, _) => capturedBooking = b)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.CreatedAt <= DateTime.UtcNow && result.CreatedAt > DateTime.UtcNow.AddSeconds(-5));
        Assert.Null(result.ProcessedAt);

        // Проверка, что бронь была передана в хранилище
        Assert.NotNull(capturedBooking);
        Assert.Equal(result.Id, capturedBooking.Id);
        _bookingStoreMock.Verify(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);

        // Проверка уменьшения количества мест
        Assert.Equal(initialSeats - 1, existingEvent.AvailableSeats);
    }

    /// <summary>
    /// Создание нескольких броней (до лимита) — все успешны, у каждой уникальный Id, места уменьшаются корректно
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_MultipleBookingsUpToLimit_AllSucceed()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const int totalSeats = 3;
        var existingEvent = new Event("Событие с местами", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), totalSeats, "Описание");
        var eventId = existingEvent.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var bookings = new List<Booking>();
        for (int i = 0; i < totalSeats; i++)
        {
            bookings.Add(await _bookingService.CreateBookingAsync(eventId, ct));
        }

        // Assert
        var ids = bookings.Select(b => b.Id).ToList();
        Assert.Equal(totalSeats, ids.Distinct().Count());
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Pending, b.Status));
        Assert.Equal(0, existingEvent.AvailableSeats);
    }

    /// <summary>
    /// Получение брони по Id
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsCorrectBooking()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var bookingId = Guid.NewGuid();
        var expectedBooking = new Booking(bookingId, Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBooking);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(bookingId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedBooking.Id, result.Id);
        Assert.Equal(expectedBooking.EventId, result.EventId);
        Assert.Equal(expectedBooking.Status, result.Status);
        Assert.Equal(expectedBooking.CreatedAt, result.CreatedAt);
        Assert.Equal(expectedBooking.ProcessedAt, result.ProcessedAt);
    }

    /// <summary>
    /// Получение брони отражает изменение статуса (после Confirm/Reject)
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetBookingByIdAsync_AfterStatusChanged_ReturnsUpdatedStatus()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var bookingId = Guid.NewGuid();
        var originalBooking = new Booking(bookingId, Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalBooking);

        // Act - получаем Pending
        var pendingResult = await _bookingService.GetBookingByIdAsync(bookingId, ct);
        Assert.Equal(BookingStatus.Pending, pendingResult!.Status);

        // Имитация подтверждения через хранилище (используем конструктор для нового объекта)
        var confirmedBooking = new Booking(
            bookingId,
            originalBooking.EventId,
            BookingStatus.Confirmed,
            originalBooking.CreatedAt,
            DateTime.UtcNow);

        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedBooking);

        // Act - получаем обновлённую бронь
        var confirmedResult = await _bookingService.GetBookingByIdAsync(bookingId, ct);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, confirmedResult!.Status);
        Assert.NotNull(confirmedResult.ProcessedAt);
    }

    /// <summary>
    /// После вызова Confirm() бронь получает статус Confirmed и ProcessedAt устанавливается в текущее время.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void Confirm_ChangesStatusToConfirmedAndSetsProcessedAt()
    {
        // Arrange
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        // Act
        booking.Confirm();

        // Assert
        Assert.Equal(BookingStatus.Confirmed, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.True(booking.ProcessedAt.Value <= DateTime.UtcNow && booking.ProcessedAt.Value > DateTime.UtcNow.AddSeconds(-5));
    }

    /// <summary>
    /// После вызова Reject() бронь получает статус Rejected и ProcessedAt устанавливается в текущее время.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void Reject_ChangesStatusToRejectedAndSetsProcessedAt()
    {
        // Arrange
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        // Act
        booking.Reject();

        // Assert
        Assert.Equal(BookingStatus.Rejected, booking.Status);
        Assert.NotNull(booking.ProcessedAt);
        Assert.True(booking.ProcessedAt.Value <= DateTime.UtcNow && booking.ProcessedAt.Value > DateTime.UtcNow.AddSeconds(-5));
    }

    /// <summary>
    /// После вызова Reject() и последующего ReleaseSeats() у события количество свободных мест восстанавливается.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public void ReleaseSeats_AfterReject_RestoresAvailableSeats()
    {
        // Arrange
        var eventEntity = new Event("Концерт", new DateTime(2026, 5, 1), new DateTime(2026, 5, 2), 5, "Описание");
        int initialSeats = eventEntity.AvailableSeats;
        eventEntity.TryReserveSeats();  // Симулируем бронирование
        Assert.Equal(initialSeats - 1, eventEntity.AvailableSeats);

        // Act
        eventEntity.ReleaseSeats();

        // Assert
        Assert.Equal(initialSeats, eventEntity.AvailableSeats);
    }

    /// <summary>
    /// После Reject() и ReleaseSeats() можно успешно создать новую бронь на то же место.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBooking_AfterRejectAndReleaseSeats_Succeeds()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var eventEntity = new Event("Концерт", new DateTime(2026, 5, 1), new DateTime(2026, 5, 2), 1, "Описание");
        var eventId = eventEntity.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventEntity);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Первая бронь успешна, места заканчиваются (AvailableSeats становится 0)
        var firstBooking = await _bookingService.CreateBookingAsync(eventId, ct);
        Assert.NotNull(firstBooking);

        // Имитация обработки в фоне: бронь отклоняется, место освобождается
        firstBooking.Reject();
        eventEntity.ReleaseSeats();

        // Обновляем мок, чтобы GetByIdAsync возвращал событие с уже освобождённым местом
        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(eventEntity);

        // Act
        var secondBooking = await _bookingService.CreateBookingAsync(eventId, ct);

        // Assert
        Assert.NotNull(secondBooking);
        Assert.NotEqual(firstBooking.Id, secondBooking.Id);
        Assert.Equal(BookingStatus.Pending, secondBooking.Status);
        Assert.Equal(0, eventEntity.AvailableSeats);
    }

    /// <summary>
    /// Создание брони для несуществующего события
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var eventId = Guid.NewGuid();
        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(eventId, ct));
        Assert.Contains(eventId.ToString(), exception.Message);
        _bookingStoreMock.Verify(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Создание брони для удалённого события
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenEventIsDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var eventId = Guid.NewGuid();
        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Event?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(eventId, ct));
        Assert.Contains(eventId.ToString(), exception.Message);
        _bookingStoreMock.Verify(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Получение брони по несуществующему Id
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetBookingByIdAsync_WhenBookingDoesNotExist_ReturnsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();
        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(nonExistentId, ct);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Создание брони при отсутствии доступных мест → NoAvailableSeatsException
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenNoSeatsAvailable_ThrowsNoAvailableSeatsException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var existingEvent = new Event("Событие", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), 1, "Описание");
        var eventId = existingEvent.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingEvent);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var firstBooking = await _bookingService.CreateBookingAsync(eventId, ct);
        var exception = await Assert.ThrowsAsync<NoAvailableSeatsException>(() => _bookingService.CreateBookingAsync(eventId, ct));

        // Assert
        Assert.NotNull(firstBooking);
        Assert.NotNull(exception);
        Assert.Equal("No available seats for this event", exception.Message);
        _bookingStoreMock.Verify(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Тест на защиту от овербукинга:
    /// Дано: событие на 5 мест, 20 конкурентных запросов.
    /// Ожидается: ровно 5 успешных броней, 15 — NoAvailableSeatsException, AvailableSeats = 0. 
    /// </summary>
    [Fact]
    [Trait("Scenario", "Concurrency")]
    public async Task CreateBookingAsync_ConcurrentRequests_NoOverbooking()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const int totalSeats = 5;
        const int concurrentRequests = 20;
        var sharedEvent = new Event("Популярное событие", new DateTime(2026, 5, 1), new DateTime(2026, 5, 2), totalSeats, "Описание");
        var eventId = sharedEvent.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedEvent);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        int successCount = 0;
        int noSeatsExceptionCount = 0;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests).Select(async _ =>
        {
            try
            {
                await _bookingService.CreateBookingAsync(eventId, ct);
                Interlocked.Increment(ref successCount);
            }
            catch (NoAvailableSeatsException)
            {
                Interlocked.Increment(ref noSeatsExceptionCount);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(totalSeats, successCount);
        Assert.Equal(concurrentRequests - totalSeats, noSeatsExceptionCount);
        Assert.Equal(0, sharedEvent.AvailableSeats);
    }

    /// <summary>
    /// Тест на уникальность Id при конкурентных запросах:
    /// Дано: событие на 10 мест, 10 одновременных запросов.
    /// Ожидается: 10 броней с уникальными Id.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Concurrency")]
    public async Task CreateBookingAsync_ConcurrentRequests_AllIdsUnique()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        const int totalSeats = 10;
        var sharedEvent = new Event("Массовое событие", new DateTime(2026, 6, 1), new DateTime(2026, 6, 2), totalSeats, "Описание");
        var eventId = sharedEvent.Id;

        _eventServiceMock
            .Setup(s => s.GetByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedEvent);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var results = new ConcurrentBag<Booking>();

        // Act
        var tasks = Enumerable.Range(0, totalSeats).Select(async _ =>
        {
            var booking = await _bookingService.CreateBookingAsync(eventId, ct);
            results.Add(booking);
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(totalSeats, results.Count);
        var ids = results.Select(b => b.Id).ToList();
        Assert.Equal(totalSeats, ids.Distinct().Count());
        Assert.Equal(0, sharedEvent.AvailableSeats);
    }
}
