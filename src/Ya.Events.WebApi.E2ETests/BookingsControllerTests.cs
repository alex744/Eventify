using System.Net;
using System.Net.Http.Json;
using Ya.Events.WebApi.DTOs.Requests;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.IntegrationTests.Fixtures;

namespace Ya.Events.WebApi.IntegrationTests;

public class BookingsControllerTests : IClassFixture<WebApiFactory>
{
    private readonly HttpClient _client;

    public BookingsControllerTests(WebApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Проверяет, что создание брони возвращает статус 202 Accepted и корректный заголовок Location.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_Returns202AndCorrectLocationHeader()
    {
        // Arrange — создаём событие с местами
        var ct = TestContext.Current.CancellationToken;
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
        Assert.Equal(Enums.BookingStatus.Pending, booking.Status);
        Assert.Equal(bookingId, booking.Id.ToString());
    }

    /// <summary>
    /// Проверяет, что попытка бронирования при отсутствии свободных мест возвращает 409 Conflict.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_WhenNoSeatsAvailable_Returns409Conflict()
    {
        // Arrange — создаём событие с одним местом
        var ct = TestContext.Current.CancellationToken;
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
        var nonExistentEventId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        // Act
        var response = await _client.PostAsync($"/events/{nonExistentEventId}/book", null, ct);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
