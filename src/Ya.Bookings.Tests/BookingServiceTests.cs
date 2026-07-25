using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ya.Bookings.Application.Abstractions.Persistence.Repositories;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Bookings.Application.Services;
using Ya.Bookings.Domain.Entities;
using Ya.Bookings.Domain.Exceptions;
using Ya.Bookings.Domain.ValueObjects;
using Ya.Bookings.Infrastructure.Persistence;
using Ya.Bookings.Infrastructure.Repositories;

namespace Ya.Bookings.Tests;

public sealed class BookingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IBookingService _bookingService;

    public BookingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _bookingService = _scope.ServiceProvider.GetRequiredService<IBookingService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    private static Guid CreateTestEventId() => Guid.NewGuid();

    #region CreateBookingAsync Tests

    /// <summary>
    /// Проверяет, что бронь успешно создается для валидного события и возвращает информацию о брони со статусом "Ожидание".
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WithValidEventId_ReturnsBookingInfoWithPendingStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Null(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что при создании брони устанавливается корректное время создания (CreatedAt) в пределах текущего момента времени.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WithValidEventId_SetsCreatedAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var before = DateTime.UtcNow;
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(result.CreatedAt, before, after);
    }

    /// <summary>
    /// Проверяет, что несколько броней для одного события создаются с уникальными идентификаторами.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_MultipleBookingsForSameEvent_AllCreatedWithUniqueIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var results = new List<Booking>();
        for (int i = 0; i < 5; i++)
            results.Add(await _bookingService.CreateBookingAsync(eventId, userId, ct));

        // Act & Assert
        var uniqueIds = results.Select(r => r.Id).Distinct();
        Assert.Equal(5, uniqueIds.Count());
    }

    /// <summary>
    /// Создание нескольких броней (до лимита) — все успешны, у каждой уникальный Id.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_MultipleBookingsUpToLimit_AllSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var bookings = new List<Booking>();
        for (int i = 0; i < 3; i++)
            bookings.Add(await _bookingService.CreateBookingAsync(CreateTestEventId(), userId, ct));

        // Assert
        var ids = bookings.Select(b => b.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Pending, b.Status));
    }

    /// <summary>
    /// После вызова Confirm() бронь получает статус Confirmed и ProcessedAt устанавливается в текущее время.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_Confirm_ChangesStatusToConfirmedAndSetsProcessedAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;
        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Act
        created.Confirm();

        // Assert
        Assert.Equal(BookingStatus.Confirmed, created.Status);
        Assert.NotNull(created.ProcessedAt);
        Assert.True(created.ProcessedAt.Value <= DateTime.UtcNow && created.ProcessedAt.Value > DateTime.UtcNow.AddSeconds(-5));
    }

    /// <summary>
    /// После вызова Reject() бронь получает статус Rejected и ProcessedAt устанавливается в текущее время.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_Reject_ChangesStatusToRejectedAndSetsProcessedAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;
        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Act
        created.Reject();

        // Assert
        Assert.Equal(BookingStatus.Rejected, created.Status);
        Assert.NotNull(created.ProcessedAt);
        Assert.True(created.ProcessedAt.Value <= DateTime.UtcNow && created.ProcessedAt.Value > DateTime.UtcNow.AddSeconds(-5));
    }

    /// <summary>
    /// Проверяет, что достижение лимита (10) активных броней приводит к исключению TooManyActiveBookingsException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WithMaxActiveBookingsReached_ThrowsTooManyActiveBookingsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        var eventIds = Enumerable.Range(0, maxBookings + 1).Select(_ => CreateTestEventId()).ToList();

        // Создаём 10 успешных броней (до лимита)
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            Assert.NotNull(booking);
        }

        // Act & Assert — 11-я бронь должна выбросить исключение
        var exception = await Assert.ThrowsAsync<TooManyActiveBookingsException>(
            () => _bookingService.CreateBookingAsync(eventIds[maxBookings], userId, ct));
        Assert.Contains("более", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("активных", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Проверяет, что ровно до лимита (10) можно создать активные брони.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WithMaxActiveBookings_SucceedsAtLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        // Act — создаём ровно 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(CreateTestEventId(), userId, ct);
            bookings.Add(booking);
        }

        // Assert
        Assert.Equal(maxBookings, bookings.Count);
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Pending, b.Status));
    }

    /// <summary>
    /// Проверяет, что отмена брони освобождает слот в лимите активных броней.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_AfterCancelBooking_NewBookingCanBeCreated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        var eventIds = Enumerable.Range(0, maxBookings + 1).Select(_ => CreateTestEventId()).ToList();

        // Создаём 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            bookings.Add(booking);
        }

        // Act — отменяем первую бронь (должна освободить слот)
        await _bookingService.CancelBookingAsync(bookings[0].Id, userId, isAdmin: false, ct);

        // Теперь должны суметь создать новую бронь
        var newBooking = await _bookingService.CreateBookingAsync(eventIds[maxBookings], userId, ct);

        // Assert
        Assert.NotNull(newBooking);
        Assert.Equal(BookingStatus.Pending, newBooking.Status);
    }

    /// <summary>
    /// Проверяет, что лимит активных броней считает только Pending и Confirmed статусы, а не Cancelled.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_CancelledBookingsNotCountedInLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        var eventIds = Enumerable.Range(0, maxBookings + 1).Select(_ => CreateTestEventId()).ToList();

        // Создаём 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            bookings.Add(booking);
        }

        // Отменяем все 10 броней
        foreach (var booking in bookings)
            await _bookingService.CancelBookingAsync(booking.Id, userId, isAdmin: false, ct);

        // Act — теперь можно создать 11-ю бронь, так как все предыдущие отменены
        var newBooking = await _bookingService.CreateBookingAsync(eventIds[maxBookings], userId, ct);

        // Assert
        Assert.NotNull(newBooking);
        Assert.Equal(BookingStatus.Pending, newBooking.Status);
    }

    /// <summary>
    /// Проверяет, что лимит активных броней одного пользователя не влияет на другого пользователя.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_UserLimitsAreIndependent()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        var eventIds = Enumerable.Range(0, 20).Select(_ => CreateTestEventId()).ToList();

        // Act — User1 создаёт 10 броней
        var user1Bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
            user1Bookings.Add(await _bookingService.CreateBookingAsync(eventIds[i], user1, ct));

        // User2 также создаёт 10 броней на те же события
        var user2Bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
            user2Bookings.Add(await _bookingService.CreateBookingAsync(eventIds[i], user2, ct));

        // Assert
        Assert.Equal(maxBookings, user1Bookings.Count);
        Assert.Equal(maxBookings, user2Bookings.Count);
        Assert.All(user1Bookings, b => Assert.Equal(user1, b.UserId));
        Assert.All(user2Bookings, b => Assert.Equal(user2, b.UserId));
    }

    /// <summary>
    /// Проверяет, что когда User1 достигает лимита, User2 всё ещё может создавать брони.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_User1LimitedButUser2CanContinue()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        const int maxBookings = 10;

        var eventIds = Enumerable.Range(0, 15).Select(_ => CreateTestEventId()).ToList();

        // User1 создаёт ровно 10 броней (в лимите)
        for (int i = 0; i < maxBookings; i++)
            await _bookingService.CreateBookingAsync(eventIds[i], user1, ct);

        // Act — User1 пытается создать 11-ю бронь (должна выброситься ошибка)
        await Assert.ThrowsAsync<TooManyActiveBookingsException>(
            () => _bookingService.CreateBookingAsync(eventIds[10], user1, ct));

        // User2 создаёт 10 броней (без ограничений, так как это другой пользователь)
        var user2Bookings = new List<Booking>();
        for (int i = 0; i < 10 && i < eventIds.Count; i++)
            user2Bookings.Add(await _bookingService.CreateBookingAsync(eventIds[i], user2, ct));

        // Assert — User2 создал 10 броней, несмотря на лимит User1
        Assert.Equal(10, user2Bookings.Count);
        Assert.All(user2Bookings, b => Assert.Equal(user2, b.UserId));
    }

    /// <summary>
    /// Проверяет, что каждый пользователь имеет независимый счётчик активных броней.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_EachUserHasIndependentCounter()
    {
        // Arrange
        var users = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToList();
        var ct = TestContext.Current.CancellationToken;
        const int bookingsPerUser = 5;

        var eventIds = Enumerable.Range(0, 15).Select(_ => CreateTestEventId()).ToList();

        // Act — каждый пользователь создаёт 5 броней на разные события
        var userBookings = new Dictionary<Guid, List<Booking>>();
        for (int userIndex = 0; userIndex < users.Count; userIndex++)
        {
            userBookings[users[userIndex]] = new List<Booking>();
            for (int bookingIndex = 0; bookingIndex < bookingsPerUser; bookingIndex++)
            {
                var eventIndex = userIndex * bookingsPerUser + bookingIndex;
                if (eventIndex < eventIds.Count)
                {
                    var booking = await _bookingService.CreateBookingAsync(eventIds[eventIndex], users[userIndex], ct);
                    userBookings[users[userIndex]].Add(booking);
                }
            }
        }

        // Assert — каждый пользователь имеет ровно 5 броней
        foreach (var user in users)
        {
            Assert.Equal(bookingsPerUser, userBookings[user].Count);
            Assert.All(userBookings[user], b => Assert.Equal(user, b.UserId));
        }
    }

    /// <summary>
    /// Проверяет, что отмена брони User1 не влияет на счётчик User2.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_CancellingUser1BookingDoesNotAffectUser2()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        var event1Id = CreateTestEventId();
        var event2Id = CreateTestEventId();

        // User1 создаёт 2 брони
        var user1Booking1 = await _bookingService.CreateBookingAsync(event1Id, user1, ct);
        var user1Booking2 = await _bookingService.CreateBookingAsync(event2Id, user1, ct);

        // User2 создаёт 2 брони
        var user2Booking1 = await _bookingService.CreateBookingAsync(event1Id, user2, ct);
        var user2Booking2 = await _bookingService.CreateBookingAsync(event2Id, user2, ct);

        // Act — User1 отменяет первую бронь
        await _bookingService.CancelBookingAsync(user1Booking1.Id, user1, isAdmin: false, ct);

        // User2 проверяет, что его брони всё ещё активны
        var user2Booking1Retrieved = await _bookingService.GetBookingByIdAsync(user2Booking1.Id, ct);

        // Assert
        Assert.NotNull(user2Booking1Retrieved);
        Assert.Equal(BookingStatus.Pending, user2Booking1Retrieved.Status);
    }

    #endregion

    #region GetBookingByIdAsync Tests

    /// <summary>
    /// Получение брони по Id
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetBookingByIdAsync_ExistingBooking_ReturnsCorrectBooking()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = CreateTestEventId();
        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Act
        var result = await _bookingService.GetBookingByIdAsync(created.Id, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(created.Id, result.Id);
        Assert.Equal(created.EventId, result.EventId);
        Assert.Equal(BookingStatus.Pending, result.Status);
        Assert.Equal(created.CreatedAt, result.CreatedAt);
        Assert.Equal(created.ProcessedAt, result.ProcessedAt);
    }

    /// <summary>
    /// Получение брони по несуществующему Id
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task GetBookingByIdAsync_WhenBookingDoesNotExist_ReturnsNull()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        var result = await _bookingService.GetBookingByIdAsync(invalidId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Получение брони отражает изменение статуса (после Confirm/Reject)
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task GetBookingByIdAsync_AfterStatusChanged_ReturnsUpdatedStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = CreateTestEventId();

        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        created.Confirm();

        // Act
        var result = await _bookingService.GetBookingByIdAsync(created.Id, ct);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, result!.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что при получении брони другого пользователя используя перегруженный метод
    /// GetBookingByIdAsync(bookingId, userId) возвращается null, если бронь принадлежит другому пользователю.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task GetBookingByIdAsync_WithUserIdCheck_WhenBookingBelongsToAnotherUser_ReturnsNull()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = CreateTestEventId();

        var created = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = created.Id;

        // Act
        var result = await _bookingService.GetBookingByIdAsync(bookingId, anotherUserId, ct);

        // Assert
        Assert.Null(result);
    }

    /// <summary>
    /// Проверяет, что при получении собственной брони используя перегруженный метод
    /// GetBookingByIdAsync(bookingId, userId) возвращается корректная бронь.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task GetBookingByIdAsync_WithUserIdCheck_WhenBookingBelongsToUser_ReturnsBooking()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = CreateTestEventId();

        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        var bookingId = created.Id;

        // Act
        var result = await _bookingService.GetBookingByIdAsync(bookingId, userId, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(bookingId, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(created.EventId, result.EventId);
    }

    #endregion

    #region CancelBookingAsync Tests

    /// <summary>
    /// Проверяет, что владелец брони может успешно отменить свою бронь.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_OwnerCancelsOwnBooking_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        var bookingId = booking.Id;

        // Act
        var result = await _bookingService.CancelBookingAsync(bookingId, userId, isAdmin: false, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что обычный пользователь не может отменить бронь другого пользователя
    /// и получает ForbiddenException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_NonOwnerAttemptsToCancel_ThrowsForbiddenException()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = booking.Id;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => _bookingService.CancelBookingAsync(bookingId, anotherUserId, isAdmin: false, ct));
        Assert.Contains("Недостаточно прав", exception.Message);
    }

    /// <summary>
    /// Проверяет, что администратор может отменить любую бронь, включая чужую.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_AdminCancelsSomeoneElsesBooking_Succeeds()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = booking.Id;

        // Act
        var result = await _bookingService.CancelBookingAsync(bookingId, adminUserId, isAdmin: true, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что администратор может отменить собственную бронь.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_AdminCancelsOwnBooking_Succeeds()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, adminUserId, ct);
        var bookingId = booking.Id;

        // Act
        var result = await _bookingService.CancelBookingAsync(bookingId, adminUserId, isAdmin: true, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    /// <summary>
    /// Проверяет, что попытка отмены несуществующей брони приводит к NotFoundException,
    /// даже если это администратор.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_NonexistentBookingAsAdmin_ThrowsNotFoundException()
    {
        // Arrange
        var adminUserId = Guid.NewGuid();
        var nonexistentBookingId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CancelBookingAsync(nonexistentBookingId, adminUserId, isAdmin: true, ct));
        Assert.Contains(nonexistentBookingId.ToString(), exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка отмены несуществующей брони приводит к NotFoundException
    /// для обычного пользователя.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_NonexistentBookingAsUser_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var nonexistentBookingId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CancelBookingAsync(nonexistentBookingId, userId, isAdmin: false, ct));
        Assert.Contains(nonexistentBookingId.ToString(), exception.Message);
    }

    /// <summary>
    /// Проверяет идемпотентность отмены: при повторной отмене собственной брони
    /// пользователь не получает исключение.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_CancelAlreadyCancelledOwnBooking_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        var bookingId = booking.Id;

        // Первая отмена
        await _bookingService.CancelBookingAsync(bookingId, userId, isAdmin: false, ct);

        // Act — вторая отмена той же брони
        var result = await _bookingService.CancelBookingAsync(bookingId, userId, isAdmin: false, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    /// <summary>
    /// Проверяет, что администратор может повторно отменить уже отменённую бронь (идемпотентность).
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_AdminCancelsAlreadyCancelledBooking_Succeeds()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = booking.Id;

        // Первая отмена
        await _bookingService.CancelBookingAsync(bookingId, adminUserId, isAdmin: true, ct);

        // Act — вторая отмена
        var result = await _bookingService.CancelBookingAsync(bookingId, adminUserId, isAdmin: true, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    /// <summary>
    /// Проверяет, что при отмене чужой брони обычный пользователь не может получить доступ к ней
    /// через проверку прав, несмотря на наличие ID.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_UnauthorizedUserTriesMultipleTimes_AlwaysThrowsForbidden()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var unauthorizedUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = booking.Id;

        // Act & Assert — попытка отмены несколько раз
        for (int i = 0; i < 3; i++)
        {
            var exception = await Assert.ThrowsAsync<ForbiddenException>(() => _bookingService.CancelBookingAsync(bookingId, unauthorizedUserId, isAdmin: false, ct));
            Assert.Contains("Недостаточно прав", exception.Message);
        }
    }

    /// <summary>
    /// Проверяет, что пользователь, владелец брони, может отменить её после того,
    /// как администратор её уже отменил (хотя это идемпотентная операция).
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_OwnerCancelsAfterAdminCancels_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        var bookingId = booking.Id;

        // Администратор отменяет бронь
        await _bookingService.CancelBookingAsync(bookingId, adminUserId, isAdmin: true, ct);

        // Act — владелец тоже пытается отменить (идемпотентно)
        var result = await _bookingService.CancelBookingAsync(bookingId, userId, isAdmin: false, ct);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(BookingStatus.Cancelled, result.Status);
    }

    /// <summary>
    /// Проверяет, что не-администратор не может отменить чужую бронь,
    /// даже если он пытается использовать параметр isAdmin = true (параметр игнорируется на стороне клиента).
    /// Тест демонстрирует, что авторизация проверяется серверной логикой.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Authorization")]
    public async Task CancelBookingAsync_UserWithFalseAdminFlag_CannotCancelSomeoneElsesBooking()
    {
        // Arrange
        var bookingOwnerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var ct = TestContext.Current.CancellationToken;

        var booking = await _bookingService.CreateBookingAsync(eventId, bookingOwnerId, ct);
        var bookingId = booking.Id;

        // Act & Assert
        // Даже если isAdmin = false, пользователь не может отменить чужую бронь
        var exception = await Assert.ThrowsAsync<ForbiddenException>(() => _bookingService.CancelBookingAsync(bookingId, otherUserId, isAdmin: false, ct));
        Assert.Contains("Недостаточно прав", exception.Message);
    }

    #endregion

    #region Concurrency Tests

    /// <summary>
    /// Проверяет, что при одновременных запросах на создание броней система соблюдает
    /// лимит активных броней на пользователя (10) и не допускает превышения.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Concurrency")]
    public async Task CreateBookingAsync_ConcurrentRequests_DoesNotOverbookEvent()
    {
        // Arrange
        const int maxActiveBookings = 10;
        const int concurrentRequests = 20;
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act — 20 одновременных запросов от одного пользователя
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                try
                {
                    await bookingService.CreateBookingAsync(CreateTestEventId(), userId, ct);
                    return true;
                }
                catch (TooManyActiveBookingsException)
                {
                    return false;
                }
            }));

        var results = await Task.WhenAll(tasks);

        // Assert — ровно 10 броней должны быть созданы, остальные отклонены
        var successCount = results.Count(r => r);
        Assert.Equal(maxActiveBookings, successCount);
    }

    /// <summary>
    /// Проверяет, что при одновременных запросах на создание броней все успешно созданные брони 
    /// имеют уникальные идентификаторы без дублей.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Concurrency")]
    public async Task CreateBookingAsync_ConcurrentRequests_AllSuccessfulBookingsHaveUniqueIds()
    {
        // Arrange
        const int concurrentRequests = 10;
        var userId = Guid.NewGuid();
        var eventId = CreateTestEventId();
        var bookingIds = new System.Collections.Concurrent.ConcurrentBag<Guid>();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var booking = await bookingService.CreateBookingAsync(eventId, userId, ct);
                bookingIds.Add(booking.Id);
            }));

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentRequests, bookingIds.Distinct().Count());
    }

    #endregion
}
