using Moq;
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
    /// Создание брони для существующего события
    /// </summary>
    /// <returns></returns>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WhenEventExists_ReturnsBookingWithPendingStatus()
    {
        // Arrange        
        var existingEvent = new Event("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), "Описание 1");
        var eventId = existingEvent.Id;
        _eventServiceMock.Setup(s => s.GetById(eventId)).Returns(existingEvent);

        Booking? capturedBooking = null;
        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Callback<Booking, CancellationToken>((b, _) => capturedBooking = b)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.True(result.CreatedAt <= DateTime.UtcNow && result.CreatedAt > DateTime.UtcNow.AddSeconds(-5));
        Assert.Null(result.ProcessedAt);

        Assert.NotNull(capturedBooking);
        Assert.Equal(result.Id, capturedBooking.Id);
        _bookingStoreMock.Verify(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Создание нескольких броней для одного события
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_GeneratesUniqueIds()
    {
        // Arrange        
        var existingEvent = new Event("Событие 1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 2), "Описание 1");
        var eventId = existingEvent.Id;
        _eventServiceMock.Setup(s => s.GetById(eventId)).Returns(existingEvent);

        _bookingStoreMock
            .Setup(s => s.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var booking1 = await _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken);
        var booking2 = await _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken);
        var booking3 = await _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(booking1.Id, booking2.Id);
        Assert.NotEqual(booking2.Id, booking3.Id);
        Assert.NotEqual(booking1.Id, booking3.Id);
    }

    /// <summary>
    /// Получение брони по Id
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsCorrectBooking()
    {
        // Arrange
        var bookingId = Guid.NewGuid();
        var expectedBooking = new Booking(bookingId, Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBooking);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(bookingId, TestContext.Current.CancellationToken);

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
        var bookingId = Guid.NewGuid();
        var originalBooking = new Booking(bookingId, Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalBooking);

        // Act - получаем Pending
        var pendingResult = await _bookingService.GetBookingByIdAsync(bookingId, TestContext.Current.CancellationToken);
        Assert.Equal(BookingStatus.Pending, pendingResult!.Status);

        // Имитируем обновление статуса
        var confirmedBooking = originalBooking with { Status = BookingStatus.Confirmed, ProcessedAt = DateTime.UtcNow };
        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(bookingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(confirmedBooking);

        // Act - получаем обновлённую бронь
        var confirmedResult = await _bookingService.GetBookingByIdAsync(bookingId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, confirmedResult!.Status);
        Assert.NotNull(confirmedResult.ProcessedAt);
    }

    /// <summary>
    /// Создание брони для несуществующего события
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenEventDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _eventServiceMock.Setup(s => s.GetById(eventId)).Returns((Event?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken));
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
        var eventId = Guid.NewGuid();
        _eventServiceMock.Setup(s => s.GetById(eventId)).Returns((Event?)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(eventId, TestContext.Current.CancellationToken));
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
        var nonExistentId = Guid.NewGuid();
        _bookingStoreMock
            .Setup(s => s.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Booking?)null);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(nonExistentId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }
}
