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
