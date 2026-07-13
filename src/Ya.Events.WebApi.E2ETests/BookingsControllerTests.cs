using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ya.Events.Application.DTOs.Bookings;
using Ya.Events.Application.DTOs.Events;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.WebApi.IntegrationTests.Fixtures;

namespace Ya.Events.WebApi.IntegrationTests;

public class BookingsControllerTests : IClassFixture<WebApiFactory>
{
    private string? _token;
    private readonly HttpClient _client;
    private readonly WebApiFactory _factory;

    public BookingsControllerTests(WebApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Инициализирует тестового пользователя и получает JWT токен для авторизации.
    /// </summary>
    private async Task InitializeUserAndAuthAsync(UserRole role, CancellationToken ct = default)
    {
        if (_token is not null)
            return;

        _token = await _factory.CreateUserAndGetTokenAsync("user", "password", role, ct);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
    }

    #region CreateBookingAsync Tests

    /// <summary>
    /// Проверяет, что создание брони возвращает статус 202 Accepted и корректный заголовок Location.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_Returns202AndCorrectLocationHeader()
    {
        // Arrange — инициализируем пользователя и авторизацию
        var ct = TestContext.Current.CancellationToken;
        await InitializeUserAndAuthAsync(UserRole.Admin, ct);

        // Создаём событие с местами
        var createEventRequest = new CreateEventRequest
        {
            Title = "Тестовое событие",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(3),
            TotalSeats = 5
        };

        var createResponse = await _client.PostAsJsonAsync("/events", createEventRequest, ct);
        createResponse.EnsureSuccessStatusCode();
        var createdEvent = await createResponse.Content.ReadFromJsonAsync<EventResponse>(ct);
        Assert.NotNull(createdEvent);

        // Act — бронируем место
        var bookResponse = await _client.PostAsync($"/events/{createdEvent.Id}/book", null, ct);

        // Assert — статус 202 Accepted
        Assert.Equal(HttpStatusCode.Accepted, bookResponse.StatusCode);

        // Проверка заголовка Location
        var locationHeader = bookResponse.Headers.Location;
        Assert.NotNull(locationHeader);
        Assert.True(locationHeader.IsAbsoluteUri);
        // Ожидаемый маршрут: /bookings/{id}
        Assert.Contains("/bookings/", locationHeader.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        var bookingId = locationHeader.AbsolutePath.Split('/').Last();
        Assert.NotEqual(Guid.Empty, Guid.Parse(bookingId));

        // Опционально: проверяем, что тело ответа содержит бронь
        var booking = await bookResponse.Content.ReadFromJsonAsync<BookingResponse>(ct);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(bookingId, booking.Id.ToString());
    }

    /// <summary>
    /// Проверяет, что попытка бронирования при отсутствии свободных мест возвращает 409 Conflict.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_WhenNoSeatsAvailable_Returns409Conflict()
    {
        // Arrange — инициализируем пользователя и авторизацию
        var ct = TestContext.Current.CancellationToken;
        await InitializeUserAndAuthAsync(UserRole.Admin, ct);

        // Создаём событие с одним местом
        var createEventRequest = new CreateEventRequest
        {
            Title = "Мероприятие без мест",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(3),
            TotalSeats = 1
        };

        var createResponse = await _client.PostAsJsonAsync("/events", createEventRequest, ct);
        createResponse.EnsureSuccessStatusCode();
        var createdEvent = await createResponse.Content.ReadFromJsonAsync<EventResponse>(ct);
        Assert.NotNull(createdEvent);

        // Бронируем единственное место
        var firstBookResponse = await _client.PostAsync($"/events/{createdEvent.Id}/book", null, ct);
        firstBookResponse.EnsureSuccessStatusCode();

        // Act — пытаемся забронировать ещё одно место
        var secondBookResponse = await _client.PostAsync($"/events/{createdEvent.Id}/book", null, ct);

        // Assert — статус 409 Conflict
        Assert.Equal(HttpStatusCode.Conflict, secondBookResponse.StatusCode);
    }

    /// <summary>
    /// Проверяет, что бронирование для несуществующего события возвращает 404 Not Found.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_WithNonExistentEvent_Returns404NotFound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await InitializeUserAndAuthAsync(UserRole.User, ct);

        var nonExistentEventId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/events/{nonExistentEventId}/book", null, ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region CancelBookingAsync Tests

    /// <summary>
    /// Проверяет, что пользователь получает 403 Forbidden при попытке отмены чужой брони.
    /// Это критический тест авторизации.
    /// </summary>
    [Fact]
    public async Task CancelBookingAsync_NonOwnerAttemptsToCancelOthersBooking_Returns403Forbidden()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;

        // Создаём событие как администратор
        var adminClient = _factory.CreateClient();
        var adminToken = await _factory.CreateUserAndGetTokenAsync("admin_user", "password", UserRole.Admin, ct);
        adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var createEventRequest = new CreateEventRequest
        {
            Title = "Событие для проверки авторизации",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(3),
            TotalSeats = 5
        };

        var createResponse = await adminClient.PostAsJsonAsync("/events", createEventRequest, ct);
        createResponse.EnsureSuccessStatusCode();
        var createdEvent = await createResponse.Content.ReadFromJsonAsync<EventResponse>(ct);
        Assert.NotNull(createdEvent);

        // Первый пользователь создаёт бронь
        var firstUserClient = _factory.CreateClient();
        var firstUserToken = await _factory.CreateUserAndGetTokenAsync("user_first", "password", UserRole.User, ct);
        firstUserClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", firstUserToken);

        var bookResponse = await firstUserClient.PostAsync($"/events/{createdEvent.Id}/book", null, ct);
        bookResponse.EnsureSuccessStatusCode();
        var booking = await bookResponse.Content.ReadFromJsonAsync<BookingResponse>(ct);
        Assert.NotNull(booking);

        // Второй пользователь пытается отменить бронь первого пользователя
        var secondUserClient = _factory.CreateClient();
        var secondUserToken = await _factory.CreateUserAndGetTokenAsync("user_second", "password", UserRole.User, ct);
        secondUserClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondUserToken);

        // Act — второй пользователь пытается отменить чужую бронь
        var cancelResponse = await secondUserClient.DeleteAsync($"/bookings/{booking.Id}", ct);

        // Assert — должны получить 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);

        // Проверяем, что в ответе есть описание ошибки
        var errorContent = await cancelResponse.Content.ReadAsStringAsync(ct);
        Assert.NotEmpty(errorContent);
    }

    #endregion
}
