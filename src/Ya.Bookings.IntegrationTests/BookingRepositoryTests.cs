using Microsoft.Extensions.DependencyInjection;
using Ya.Bookings.Application.Abstractions.Persistence.Repositories;
using Ya.Bookings.Domain.Entities;
using Ya.Bookings.Domain.ValueObjects;
using Ya.Bookings.IntegrationTests.Fixtures;

namespace Ya.Bookings.IntegrationTests;

[Collection("PostgreSQL collection")]
public sealed class BookingRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly IBookingRepository _bookingRepository;

    public BookingRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _bookingRepository = fixture.ServiceProvider.GetRequiredService<IBookingRepository>();
    }

    private static Booking CreateTestBooking(Guid? eventId = null, Guid? userId = null)
    {
        return Booking.CreatePending(
            eventId ?? Guid.NewGuid(),
            userId ?? Guid.NewGuid());
    }

    #region CreateAsync Tests

    /// <summary>
    /// Проверяет, что бронь успешно создаётся и возвращается с корректными данными.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task CreateAsync_WithValidBooking_ReturnsCreatedBooking()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var booking = CreateTestBooking();

        // Act
        var result = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Null(result.ProcessedAt);
        Assert.NotEqual(Guid.Empty, result.EventId);
        Assert.NotEqual(Guid.Empty, result.UserId);
    }

    /// <summary>
    /// Проверяет, что несколько броней могут быть созданы и все корректно сохранены в БД.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task CreateAsync_MultipleBookings_AllPersisted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var bookings = Enumerable.Range(0, 5)
            .Select(_ => CreateTestBooking(eventId, userId))
            .ToList();

        // Act
        var results = new List<Booking>();
        foreach (var booking in bookings)
        {
            results.Add(await _bookingRepository.CreateAsync(booking, CancellationToken.None));
        }

        // Assert
        Assert.Equal(5, results.Count);
        Assert.All(results, r => Assert.NotEqual(Guid.Empty, r.Id));
        Assert.All(results, r => Assert.Equal(eventId, r.EventId));
        Assert.All(results, r => Assert.Equal(userId, r.UserId));
    }

    #endregion

    #region GetByIdAsync Tests

    /// <summary>
    /// Проверяет, что брони могут быть получены по идентификатору и содержат корректные данные.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetByIdAsync_WithExistingId_ReturnsBooking()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var booking = CreateTestBooking();
        var created = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.EventId, result.EventId);
        Assert.Equal(created.UserId, result.UserId);
        Assert.Equal(BookingStatus.Pending, result.Status);
    }

    /// <summary>
    /// Проверяет, что при запросе несуществующей брони возвращается null.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var result = await _bookingRepository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что изменение статуса брони сохраняется и отражается при повторном получении.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetByIdAsync_AfterStatusChange_ReturnsUpdatedStatus()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var created = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);

        // Изменяем статус и сохраняем
        created.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    #endregion

    #region GetPendingBookingIdsAsync Tests

    /// <summary>
    /// Проверяет, что метод GetPendingBookingIdsAsync возвращает пустой список, если нет ожидающих подтверждения броней.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetPendingBookingIdsAsync_WithNoPendingBookings_ReturnsEmptyList()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var result = await _bookingRepository.GetPendingBookingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    /// <summary>
    /// Проверяет, что метод GetPendingBookingIdsAsync возвращает все идентификаторы ожидающих подтверждения броней, если они существуют.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetPendingBookingIdsAsync_WithMultiplePendingBookings_ReturnsAllIds()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var created = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);
            ids.Add(created.Id);
        }

        // Act
        var result = await _bookingRepository.GetPendingBookingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(ids.OrderBy(x => x), result.OrderBy(x => x));
    }

    /// <summary>
    /// Проверяет, что метод GetPendingBookingIdsAsync игнорирует брони с другими статусами, кроме ожидающих подтверждения.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetPendingBookingIdsAsync_IgnoresNonPendingBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        var pending = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);

        var confirmed = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);
        confirmed.Confirm();

        var rejected = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);
        rejected.Reject();

        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetPendingBookingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(pending.Id, result[0]);
    }

    #endregion

    #region CountActiveBookingsAsync Tests

    /// <summary>
    /// Проверяет, что метод CountActiveBookingsAsync возвращает 0, если у пользователя нет активных броней.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task CountActiveBookingsAsync_WithNoBookings_ReturnsZero()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        // Act
        var count = await _bookingRepository.CountActiveBookingsAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Проверяет, что метод CountActiveBookingsAsync учитывает только брони со статусами "Ожидает подтверждения" и "Подтверждена".
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task CountActiveBookingsAsync_CountsOnlyPendingAndConfirmed()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        var pending = await _bookingRepository.CreateAsync(CreateTestBooking(userId: userId), CancellationToken.None);
        var confirmed = await _bookingRepository.CreateAsync(CreateTestBooking(userId: userId), CancellationToken.None);
        var rejected = await _bookingRepository.CreateAsync(CreateTestBooking(userId: userId), CancellationToken.None);
        var cancelled = await _bookingRepository.CreateAsync(CreateTestBooking(userId: userId), CancellationToken.None);

        confirmed.Confirm();
        rejected.Reject();
        cancelled.Cancel();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Act
        var count = await _bookingRepository.CountActiveBookingsAsync(userId, CancellationToken.None);

        // Assert
        Assert.Equal(2, count); // Pending + Confirmed
    }

    /// <summary>
    /// Проверяет, что метод CountActiveBookingsAsync не учитывает брони других пользователей.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task CountActiveBookingsAsync_DoesNotCountOtherUsersBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await _bookingRepository.CreateAsync(CreateTestBooking(userId: userA), CancellationToken.None);

        var bookingForB = await _bookingRepository.CreateAsync(CreateTestBooking(userId: userB), CancellationToken.None);
        bookingForB.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Act
        var countA = await _bookingRepository.CountActiveBookingsAsync(userA, CancellationToken.None);
        var countB = await _bookingRepository.CountActiveBookingsAsync(userB, CancellationToken.None);

        // Assert
        Assert.Equal(1, countA);
        Assert.Equal(1, countB);
    }

    #endregion

    #region SaveChangesAsync Tests

    /// <summary>
    /// Проверяет, что изменения в статусе брони сохраняются в базе данных после вызова SaveChangesAsync.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var created = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);

        // Act
        created.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var result = await _bookingRepository.GetByIdAsync(created.Id, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что метод SaveChangesAsync сохраняет все изменения в базе данных.
    /// </summary>    
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task SaveChangesAsync_WithMultipleChanges_AllPersisted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();

        var booking1 = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);
        var booking2 = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);
        var booking3 = await _bookingRepository.CreateAsync(CreateTestBooking(), CancellationToken.None);

        // Act
        booking1.Confirm();
        booking2.Reject();
        booking3.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Assert
        var result1 = await _bookingRepository.GetByIdAsync(booking1.Id, CancellationToken.None);
        var result2 = await _bookingRepository.GetByIdAsync(booking2.Id, CancellationToken.None);
        var result3 = await _bookingRepository.GetByIdAsync(booking3.Id, CancellationToken.None);

        Assert.Equal(BookingStatus.Confirmed, result1!.Status);
        Assert.Equal(BookingStatus.Rejected, result2!.Status);
        Assert.Equal(BookingStatus.Confirmed, result3!.Status);
    }

    #endregion
}
