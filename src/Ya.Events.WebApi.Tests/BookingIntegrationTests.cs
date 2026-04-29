using System.Net;
using System.Net.Http.Json;
using Ya.Events.WebApi.DTOs.Requests;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Tests.Fixtures;

namespace Ya.Events.WebApi.Tests;

public class BookingIntegrationTests : IClassFixture<WebApiFactory>
{
    private readonly HttpClient _client;

    public BookingIntegrationTests(WebApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateBooking_Returns202AndCorrectLocationHeader()
    {
        // Arrange — создаём событие с местами
        var ct = TestContext.Current.CancellationToken;
        var createEventRequest = new CreateEventRequest
        {
            Title = "Тестовое событие",
            StartAt = new DateTime(2026, 1, 1),
            EndAt = new DateTime(2026, 1, 2),
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
}
