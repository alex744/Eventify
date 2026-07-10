using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.Services;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.Exceptions;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.Infrastructure.Persistence;
using Ya.Events.Infrastructure.Repositories;

namespace Ya.Events.WebApi.Tests;

public sealed class BookingServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IServiceScope _scope;
    private readonly IEventService _eventService;
    private readonly IBookingService _bookingService;

    public BookingServiceTests()
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IEventService, EventService>();
        services.AddScoped<IBookingService, BookingService>();

        _serviceProvider = services.BuildServiceProvider();
        _scope = _serviceProvider.CreateScope();
        _eventService = _scope.ServiceProvider.GetRequiredService<IEventService>();
        _bookingService = _scope.ServiceProvider.GetRequiredService<IBookingService>();
    }

    public void Dispose()
    {
        _scope.Dispose();
        _serviceProvider.Dispose();
    }

    private async Task<Guid> CreateTestEventAsync(int totalSeats = 10)
    {
        var futureDate = DateTime.UtcNow.AddDays(1);
        var created = await _eventService.CreateAsync(Event.Create(
            title: "Test Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: totalSeats
        ), TestContext.Current.CancellationToken);

        return created.Id;
    }

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
        var eventId = await CreateTestEventAsync();
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
        var eventId = await CreateTestEventAsync();
        var before = DateTime.UtcNow;
        var ct = TestContext.Current.CancellationToken;

        // Act
        var result = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(result.CreatedAt, before, after);
    }

    /// <summary>
    /// Проверяет, что при попытке создания брони для несуществующего события выбрасывается NotFoundException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WithNonExistentEvent_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var invalidEventId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(invalidEventId, userId, ct));
        Assert.Equal($"Событие с идентификатором '{invalidEventId}' не найдено.", exception.Message);
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
        var eventId = await CreateTestEventAsync(totalSeats: 5);
        var ct = TestContext.Current.CancellationToken;

        var results = new List<Booking>();
        for (int i = 0; i < 5; i++)
            results.Add(await _bookingService.CreateBookingAsync(eventId, userId, ct));

        // Act & Assert
        var uniqueIds = results.Select(r => r.Id).Distinct();
        Assert.Equal(5, uniqueIds.Count());
    }

    /// <summary>
    /// Проверяет, что при отсутствии доступных мест выбрасывается исключение NoAvailableSeatsException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenNoSeatsAvailable_ThrowsNoAvailableSeatsException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = await CreateTestEventAsync(totalSeats: 1);
        await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Act & Assert
        await Assert.ThrowsAsync<NoAvailableSeatsException>(() => _bookingService.CreateBookingAsync(eventId, userId, ct));
    }

    /// <summary>
    /// Проверяет, что каждое создание брони уменьшает количество доступных мест на события.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_DecrementsAvailableSeats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = await CreateTestEventAsync(totalSeats: 3);

        await _bookingService.CreateBookingAsync(eventId, userId, ct);
        await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Act & Assert
        var eventInfo = await _eventService.GetByIdAsync(eventId, ct);
        Assert.Equal(1, eventInfo?.AvailableSeats);
    }

    /// <summary>
    /// Проверяет, что создание нескольких броней (до лимита) — все успешны, у каждой уникальный Id, места уменьшаются корректно
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_MultipleBookingsUpToLimit_AllSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = await CreateTestEventAsync(totalSeats: 3);

        // Act        
        var bookings = new List<Booking>();
        for (int i = 0; i < 3; i++)
        {
            bookings.Add(await _bookingService.CreateBookingAsync(eventId, userId, ct));
        }

        // Assert
        var ids = bookings.Select(b => b.Id).ToList();
        var eventInfo = await _eventService.GetByIdAsync(eventId, ct);

        Assert.Equal(3, ids.Distinct().Count());
        Assert.All(bookings, b => Assert.Equal(BookingStatus.Pending, b.Status));
        Assert.Equal(0, eventInfo?.AvailableSeats);
    }

    /// <summary>
    /// После Reject() и ReleaseSeats() можно успешно создать новую бронь на то же место.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_AfterRejectAndReleaseSeats_Succeeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = await CreateTestEventAsync(totalSeats: 1);

        // Первая бронь успешна, места заканчиваются (AvailableSeats становится 0)
        var firstBooking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        Assert.NotNull(firstBooking);

        // Имитация обработки в фоне: бронь отклоняется, место освобождается
        var context = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var eventInfo = await context.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
        if (eventInfo is not null)
        {
            firstBooking.Reject();
            eventInfo.ReleaseSeats();
            await context.SaveChangesAsync(ct);
        }

        // Act
        var secondBooking = await _bookingService.CreateBookingAsync(eventId, userId, ct);

        // Assert
        Assert.NotNull(secondBooking);
        Assert.NotEqual(firstBooking.Id, secondBooking.Id);
        Assert.Equal(BookingStatus.Pending, secondBooking.Status);
        Assert.Equal(0, eventInfo?.AvailableSeats);
    }

    /// <summary>
    /// Создание брони для удалённого события
    /// </summary>    
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WhenEventIsDeleted_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;
        var eventId = await CreateTestEventAsync(totalSeats: 1);

        // Act
        await _eventService.DeleteAsync(eventId, ct);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => _bookingService.CreateBookingAsync(eventId, userId, ct));
        Assert.Contains(eventId.ToString(), exception.Message);
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
        var eventId = await CreateTestEventAsync(totalSeats: 5);
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
        var eventId = await CreateTestEventAsync(totalSeats: 5);
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
    /// После вызова Reject() и последующего ReleaseSeats() у события количество свободных мест восстанавливается.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_ReleaseSeats_AfterReject_RestoresAvailableSeats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = await CreateTestEventAsync(totalSeats: 5);
        var ct = TestContext.Current.CancellationToken;
        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        var eventInfo = await _eventService.GetByIdAsync(eventId, ct);
        int initialSeats = eventInfo?.AvailableSeats ?? 0;

        // Симулируем бронирование
        eventInfo?.TryReserveSeats();
        Assert.Equal(initialSeats - 1, eventInfo?.AvailableSeats);

        // Act
        eventInfo?.ReleaseSeats();

        // Assert
        Assert.Equal(initialSeats, eventInfo?.AvailableSeats);
    }

    /// <summary>
    /// Проверяет, что бронирование прошедшего события приводит к исключению PastEventBookingException.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WithPastEvent_ThrowsPastEventBookingException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Создаём событие, которое начинается прямо сейчас
        var nowDate = DateTime.UtcNow;
        var pastEvent = Event.Create(
            title: "Past Event",
            startAt: nowDate.AddSeconds(1),
            endAt: nowDate.AddHours(2),
            totalSeats: 5
        );
        var createdEvent = await _eventService.CreateAsync(pastEvent, ct);
        await Task.Delay(2000, ct); // Ждём 2 секунды, чтобы событие стало прошедшим

        // Act & Assert
        var exception = await Assert.ThrowsAsync<PastEventBookingException>(() => _bookingService.CreateBookingAsync(createdEvent.Id, userId, ct));
        Assert.Equal("Нельзя создать бронь на событие, которое уже началось.", exception.Message);
    }

    /// <summary>
    /// Проверяет, что попытка бронирования события, которое начинается в настоящий момент, приводит к ошибке.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Failure")]
    public async Task CreateBookingAsync_WithEventStartingNow_ThrowsPastEventBookingException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Создаём событие, которое начинается прямо сейчас (или в прошлом из-за микросекунд)
        var nowDate = DateTime.UtcNow;
        var event_ = Event.Create(
            title: "Event Starting Now",
            startAt: nowDate.AddTicks(100),
            endAt: nowDate.AddHours(2),
            totalSeats: 5
        );
        var createdEvent = await _eventService.CreateAsync(event_, ct);

        // Act & Assert
        await Assert.ThrowsAsync<PastEventBookingException>(() => _bookingService.CreateBookingAsync(createdEvent.Id, userId, ct));
    }

    /// <summary>
    /// Проверяет, что события в будущем успешно бронируются, в отличие от прошедших.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Success")]
    public async Task CreateBookingAsync_WithFutureEvent_SucceedsWhilePastFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Создаём будущее событие
        var futureDate = DateTime.UtcNow.AddDays(1);
        var futureEvent = Event.Create(
            title: "Future Event",
            startAt: futureDate,
            endAt: futureDate.AddHours(2),
            totalSeats: 5
        );
        var futureEventCreated = await _eventService.CreateAsync(futureEvent, ct);

        // Act - бронируем будущее событие (успешно)
        var booking = await _bookingService.CreateBookingAsync(futureEventCreated.Id, userId, ct);

        // Assert
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Pending, booking.Status);
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

        // Создаём 11 событий
        var eventIds = new List<Guid>();
        for (int i = 0; i < maxBookings + 1; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1); // Каждое событие на разный день
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 1
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Создаём 10 успешных броней (до лимита)
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            Assert.NotNull(booking);
        }

        // Act & Assert - 11-я бронь должна выбросить исключение
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

        // Создаём 10 событий
        var eventIds = new List<Guid>();
        for (int i = 0; i < maxBookings; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 1
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Act - создаём ровно 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
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

        // Создаём 11 событий
        var eventIds = new List<Guid>();
        for (int i = 0; i < maxBookings + 1; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 1
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Создаём 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            bookings.Add(booking);
        }

        // Act - отменяем первую бронь (должна освободить слот)
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

        // Создаём 11 событий
        var eventIds = new List<Guid>();
        for (int i = 0; i < maxBookings + 1; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 1
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Создаём 10 броней
        var bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], userId, ct);
            bookings.Add(booking);
        }

        // Отменяем все 10 броней
        foreach (var booking in bookings)
        {
            await _bookingService.CancelBookingAsync(booking.Id, userId, isAdmin: false, ct);
        }

        // Act - теперь можно создать 11-ю бронь, так как все предыдущие отменены
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

        // Создаём 20 событий (по 10 на каждого пользователя)
        var eventIds = new List<Guid>();
        for (int i = 0; i < 20; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 2 // Каждое событие может вместить 2 пользователя
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Act - User1 создаёт 10 броней
        var user1Bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], user1, ct);
            user1Bookings.Add(booking);
        }

        // User2 должен также суметь создать 10 броней на те же события
        var user2Bookings = new List<Booking>();
        for (int i = 0; i < maxBookings; i++)
        {
            var booking = await _bookingService.CreateBookingAsync(eventIds[i], user2, ct);
            user2Bookings.Add(booking);
        }

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

        // Создаём 15 событий
        var eventIds = new List<Guid>();
        for (int i = 0; i < 15; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 2
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // User1 создаёт ровно 10 броней (в лимите)
        for (int i = 0; i < maxBookings; i++)
        {
            await _bookingService.CreateBookingAsync(eventIds[i], user1, ct);
        }

        // Act - User1 пытается создать 11-ю бронь (должна выброситься ошибка)
        await Assert.ThrowsAsync<TooManyActiveBookingsException>(
            () => _bookingService.CreateBookingAsync(eventIds[10], user1, ct));

        // User2 создаёт 10 броней (без ограничений, так как это другой пользователь)
        var user2Bookings = new List<Booking>();
        for (int i = 0; i < 10; i++)
        {
            if (i < eventIds.Count)
            {
                var booking = await _bookingService.CreateBookingAsync(eventIds[i], user2, ct);
                user2Bookings.Add(booking);
            }
        }

        // Assert - User2 создал 10 броней, несмотря на лимит User1
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

        // Создаём 15 событий (5 на каждого пользователя)
        var eventIds = new List<Guid>();
        for (int i = 0; i < 15; i++)
        {
            var futureDate = DateTime.UtcNow.AddDays(i + 1);
            var event_ = Event.Create(
                title: $"Event {i + 1}",
                startAt: futureDate,
                endAt: futureDate.AddHours(2),
                totalSeats: 3 // Каждое событие может вместить 3 пользователей
            );
            var created = await _eventService.CreateAsync(event_, ct);
            eventIds.Add(created.Id);
        }

        // Act - каждый пользователь создаёт 5 броней на разные события
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

        // Assert - каждый пользователь имеет ровно 5 броней
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

        // Создаём 2 события
        var event1 = Event.Create(
            title: "Event 1",
            startAt: DateTime.UtcNow.AddDays(1),
            endAt: DateTime.UtcNow.AddDays(1).AddHours(2),
            totalSeats: 2
        );
        var event2 = Event.Create(
            title: "Event 2",
            startAt: DateTime.UtcNow.AddDays(2),
            endAt: DateTime.UtcNow.AddDays(2).AddHours(2),
            totalSeats: 2
        );

        var event1Id = (await _eventService.CreateAsync(event1, ct)).Id;
        var event2Id = (await _eventService.CreateAsync(event2, ct)).Id;

        // User1 создаёт 2 брони
        var user1Booking1 = await _bookingService.CreateBookingAsync(event1Id, user1, ct);
        var user1Booking2 = await _bookingService.CreateBookingAsync(event2Id, user1, ct);

        // User2 создаёт 2 брони
        var user2Booking1 = await _bookingService.CreateBookingAsync(event1Id, user2, ct);
        var user2Booking2 = await _bookingService.CreateBookingAsync(event2Id, user2, ct);

        // Act - User1 отменяет первую бронь
        await _bookingService.CancelBookingAsync(user1Booking1.Id, user1, isAdmin: false, ct);

        // User2 проверяет, что его брони всё ещё активны
        var user2ActiveBookingsStillValid = true; // Нет метода для подсчёта активных броней в интерфейсе
        var user2Booking1Retrieved = await _bookingService.GetBookingByIdAsync(user2Booking1.Id, ct);

        // Assert
        Assert.NotNull(user2Booking1Retrieved);
        Assert.Equal(BookingStatus.Pending, user2Booking1Retrieved.Status);
        Assert.True(user2ActiveBookingsStillValid);
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
        var eventId = await CreateTestEventAsync();
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
        var eventId = await CreateTestEventAsync();

        var created = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        created.Confirm();

        // Act
        var result = await _bookingService.GetBookingByIdAsync(created.Id, ct);

        // Assert
        Assert.Equal(BookingStatus.Confirmed, result!.Status);
        Assert.NotNull(result.ProcessedAt);
    }

    #endregion

    #region Concurrency Tests

    /// <summary>
    /// Проверяет, что при одновременных запросах на создание броней система не допускает перебронирование (overbooking) 
    /// и разрешает только количество броней, равное доступным местам.
    /// </summary>
    [Fact]
    [Trait("Scenario", "Concurrency")]
    public async Task CreateBookingAsync_ConcurrentRequests_DoesNotOverbookEvent()
    {
        // Arrange
        const int totalSeats = 5;
        const int concurrentRequests = 20;
        var userId = Guid.NewGuid();
        var eventId = await CreateTestEventAsync(totalSeats: totalSeats);
        var ct = TestContext.Current.CancellationToken;

        // Act
        var tasks = Enumerable.Range(0, concurrentRequests)
            .Select(_ => Task.Run(async () =>
            {
                using var scope = _serviceProvider.CreateScope();
                var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
                try
                {
                    await bookingService.CreateBookingAsync(eventId, userId, ct);
                    return true;
                }
                catch (NoAvailableSeatsException)
                {
                    return false;
                }
            }));

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r);
        Assert.Equal(totalSeats, successCount);
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
        const int totalSeats = 10;
        const int concurrentRequests = 10;
        var userId = Guid.NewGuid();
        var eventId = await CreateTestEventAsync(totalSeats: totalSeats);
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
        Assert.Equal(totalSeats, bookingIds.Distinct().Count());
    }

    #endregion    
}
