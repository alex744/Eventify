using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Exceptions;
using Ya.Events.WebApi.Models;
using Ya.Events.WebApi.Stores;

namespace Ya.Events.WebApi.Tests;

public class InMemoryBookingStoreTests
{
    private readonly InMemoryBookingStore _store = new();

    /// <summary>
    /// Проверяет, что новое бронирование успешно добавляется в хранилище
    /// и может быть получено по идентификатору.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task AddAsync_NewBooking_AddsSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        // Act
        await _store.AddAsync(booking, ct);
        var retrieved = await _store.GetByIdAsync(booking.Id, ct);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(booking.Id, retrieved.Id);
    }

    /// <summary>
    /// Проверяет, что при запросе по существующему идентификатору возвращается
    /// корректное бронирование со всеми совпадающими полями.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetByIdAsync_ExistingId_ReturnsBooking()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);
        await _store.AddAsync(booking, ct);

        // Act
        var result = await _store.GetByIdAsync(booking.Id, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(booking.Id, result.Id);
        Assert.Equal(booking.EventId, result.EventId);
        Assert.Equal(booking.Status, result.Status);
        Assert.Equal(booking.CreatedAt, result.CreatedAt);
        Assert.Equal(booking.ProcessedAt, result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что обновление существующего бронирования изменяет его статус
    /// и устанавливает время обработки.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task UpdateAsync_ExistingBooking_UpdatesSuccessfully()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);
        await _store.AddAsync(booking, ct);

        var updatedBooking = new Booking(
            booking.Id,
            booking.EventId,
            BookingStatus.Confirmed,
            booking.CreatedAt,
            DateTime.UtcNow);

        // Act
        await _store.UpdateAsync(updatedBooking, ct);
        var result = await _store.GetByIdAsync(booking.Id, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что метод GetAllPendingAsync возвращает только бронирования
    /// со статусом Pending, игнорируя остальные статусы.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetAllPendingAsync_ReturnsOnlyPendingBookings()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var pending1 = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);
        var pending2 = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);
        var confirmed = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Confirmed, DateTime.UtcNow);
        var rejected = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Rejected, DateTime.UtcNow);

        await _store.AddAsync(pending1, ct);
        await _store.AddAsync(pending2, ct);
        await _store.AddAsync(confirmed, ct);
        await _store.AddAsync(rejected, ct);

        // Act
        var pendingList = await _store.GetAllPendingAsync(ct);

        // Assert
        Assert.Equal(2, pendingList.Count);
        Assert.Contains(pendingList, b => b.Id == pending1.Id);
        Assert.Contains(pendingList, b => b.Id == pending2.Id);
    }

    /// <summary>
    /// Проверяет, что попытка добавить бронирование с уже существующим идентификатором
    /// вызывает исключение InvalidOperationException с соответствующим сообщением.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task AddAsync_DuplicateId_ThrowsInvalidOperationException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var booking1 = new Booking(id, Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);
        var booking2 = new Booking(id, Guid.NewGuid(), BookingStatus.Confirmed, DateTime.UtcNow);

        // Act
        await _store.AddAsync(booking1, ct);

        // Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _store.AddAsync(booking2, ct));
        Assert.Contains(id.ToString(), ex.Message);
    }

    /// <summary>
    /// Проверяет, что запрос бронирования с несуществующим идентификатором
    /// возвращает null.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _store.GetByIdAsync(nonExistentId, ct);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что попытка обновить несуществующее бронирование
    /// приводит к исключению NotFoundException с указанием идентификатора.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task UpdateAsync_NonExistentBooking_ThrowsNotFoundException()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var nonExistentId = Guid.NewGuid();
        var booking = new Booking(nonExistentId, Guid.NewGuid(), BookingStatus.Confirmed, DateTime.UtcNow);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _store.UpdateAsync(booking, ct));
        Assert.Contains(nonExistentId.ToString(), ex.Message);
    }

    /// <summary>
    /// Проверяет, что при отменённом токене метод AddAsync выбрасывает
    /// OperationCanceledException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task AddAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var ct = new CancellationToken(canceled: true);
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Pending, DateTime.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _store.AddAsync(booking, ct));
    }

    /// <summary>
    /// Проверяет, что при отменённом токене метод GetByIdAsync выбрасывает
    /// OperationCanceledException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetByIdAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var ct = new CancellationToken(canceled: true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _store.GetByIdAsync(Guid.NewGuid(), ct));
    }

    /// <summary>
    /// Проверяет, что при отменённом токене метод UpdateAsync выбрасывает
    /// OperationCanceledException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task UpdateAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var ct = new CancellationToken(canceled: true);
        var booking = new Booking(Guid.NewGuid(), Guid.NewGuid(), BookingStatus.Confirmed, DateTime.UtcNow);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _store.UpdateAsync(booking, ct));
    }

    /// <summary>
    /// Проверяет, что при отменённом токене метод GetAllPendingAsync выбрасывает
    /// OperationCanceledException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetAllPendingAsync_WithCancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var ct = new CancellationToken(canceled: true);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => _store.GetAllPendingAsync(ct));
    }
}
