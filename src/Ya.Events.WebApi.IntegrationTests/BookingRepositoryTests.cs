using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.WebApi.IntegrationTests.Fixtures;

namespace Ya.Events.WebApi.IntegrationTests;

[Collection("PostgreSQL collection")]
public sealed class BookingRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly IBookingRepository _bookingRepository;
    private readonly IEventRepository _eventRepository;
    private readonly IUserRepository _userRepository;

    public BookingRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _bookingRepository = fixture.ServiceProvider.GetRequiredService<IBookingRepository>();
        _eventRepository = fixture.ServiceProvider.GetRequiredService<IEventRepository>();
        _userRepository = fixture.ServiceProvider.GetRequiredService<IUserRepository>();
    }

    /// <summary>
    /// Создаёт тестовое событие.
    /// </summary>
    private async Task<Event> CreateTestEventAsync(int totalSeats = 10)
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        return await _eventRepository.CreateAsync(Event.Create(
            title: "Test Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: totalSeats
        ), CancellationToken.None);
    }

    private async Task<Guid> CreateTestUserAsync()
    {
        var user = User.Create("test", "test", UserRole.User);
        await _userRepository.CreateAsync(user, CancellationToken.None);

        return user.Id;
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
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync();
        var booking = Booking.CreatePending(@event.Id, userId);

        // Act
        var result = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        // Assert
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(@event.Id, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Null(result.ProcessedAt);
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
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync(totalSeats: 5);
        var bookings = Enumerable.Range(1, 5)
            .Select(_ => Booking.CreatePending(@event.Id, userId))
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
        Assert.All(results, r => Assert.Equal(@event.Id, r.EventId));
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
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync();
        var booking = Booking.CreatePending(@event.Id, userId);
        var created = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.EventId, result.EventId);
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
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _bookingRepository.GetByIdAsync(nonExistentId, CancellationToken.None);

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
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync();
        var booking = Booking.CreatePending(@event.Id, userId);
        var created = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

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

    #region GetEventByIdAsync Tests

    /// <summary>
    /// Проверяет, что событие может быть получено по идентификатору через репозиторий броней.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetEventByIdAsync_WithExistingEventId_ReturnsEvent()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var @event = await CreateTestEventAsync();

        // Act
        var result = await _bookingRepository.GetEventByIdAsync(@event.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(@event.Id, result.Id);
        Assert.Equal(@event.Title, result.Title);
    }

    /// <summary>
    /// Проверяет, что при запросе несуществующего события возвращается null.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetEventByIdAsync_WithNonExistentEventId_ReturnsNull()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _bookingRepository.GetEventByIdAsync(nonExistentId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что количество доступных мест события корректно отражает зарезервированные места.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetEventByIdAsync_ReturnsEventWithCorrectAvailableSeats()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync(totalSeats: 10);
        var booking = Booking.CreatePending(@event.Id, userId);
        await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        @event.TryReserveSeats();
        await _eventRepository.UpdateAsync(@event.Id, @event, CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetEventByIdAsync(@event.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(9, result.AvailableSeats);
    }

    #endregion

    #region GetPendingBookingIdsAsync Tests

    /// <summary>
    /// Проверяет, что при отсутствии ожидающих броней возвращается пустой список.
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
    /// Проверяет, что все ID ожидающих броней возвращаются корректно.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetPendingBookingIdsAsync_WithMultiplePendingBookings_ReturnsAllIds()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync(totalSeats: 5);
        var bookingIds = new List<Guid>();

        for (int i = 0; i < 3; i++)
        {
            var booking = Booking.CreatePending(@event.Id, userId);
            var created = await _bookingRepository.CreateAsync(booking, CancellationToken.None);
            bookingIds.Add(created.Id);
        }

        // Act
        var result = await _bookingRepository.GetPendingBookingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(bookingIds.OrderBy(x => x), result.OrderBy(x => x));
    }

    /// <summary>
    /// Проверяет, что в результат включаются только брони со статусом Pending, остальные игнорируются.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task GetPendingBookingIdsAsync_IgnoresNonPendingBookings()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync(totalSeats: 5);

        // Создаём pending бронь
        var pendingBooking = Booking.CreatePending(@event.Id, userId);
        var createdPending = await _bookingRepository.CreateAsync(pendingBooking, CancellationToken.None);

        // Создаём и подтверждаем бронь
        var confirmedBooking = Booking.CreatePending(@event.Id, userId);
        var createdConfirmed = await _bookingRepository.CreateAsync(confirmedBooking, CancellationToken.None);
        createdConfirmed.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Создаём и отклоняем бронь
        var rejectedBooking = Booking.CreatePending(@event.Id, userId);
        var createdRejected = await _bookingRepository.CreateAsync(rejectedBooking, CancellationToken.None);
        createdRejected.Reject();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Act
        var result = await _bookingRepository.GetPendingBookingIdsAsync(CancellationToken.None);

        // Assert
        Assert.Single(result);
        Assert.Equal(createdPending.Id, result.First());
    }

    #endregion

    #region SaveChangesAsync Tests

    /// <summary>
    /// Проверяет, что изменения в объектах броней корректно сохраняются в БД.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync();
        var booking = Booking.CreatePending(@event.Id, userId);
        var created = await _bookingRepository.CreateAsync(booking, CancellationToken.None);

        // Act
        created.Confirm();
        await _bookingRepository.SaveChangesAsync(CancellationToken.None);

        // Verify by fetching fresh instance
        var result = await _bookingRepository.GetByIdAsync(created.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Confirmed, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что несколько изменений разных броней сохраняются одновременно и корректно.
    /// </summary>
    [Fact]
    [Trait("Category", "BookingRepository")]
    public async Task SaveChangesAsync_WithMultipleChanges_AllPersisted()
    {
        // Arrange
        await _fixture.ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var @event = await CreateTestEventAsync(totalSeats: 3);

        // Создаём три брони
        var booking1 = await _bookingRepository.CreateAsync(Booking.CreatePending(@event.Id, userId), CancellationToken.None);
        var booking2 = await _bookingRepository.CreateAsync(Booking.CreatePending(@event.Id, userId), CancellationToken.None);
        var booking3 = await _bookingRepository.CreateAsync(Booking.CreatePending(@event.Id, userId), CancellationToken.None);

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
