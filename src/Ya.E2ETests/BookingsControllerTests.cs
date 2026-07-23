using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ya.Bookings.Application.DTOs;
using Ya.Bookings.Domain.ValueObjects;
using Ya.E2ETests.Fixtures;
using Ya.Events.Application.DTOs;
using Ya.Users.Domain.ValueObjects;

namespace Ya.E2ETests;

public class BookingsControllerTests : IClassFixture<WebApiFactory>
{
    private readonly WebApiFactory _factory;

    public BookingsControllerTests(WebApiFactory factory)
    {
        _factory = factory;
    }

    private static void SetBearer(HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Проверяет, что создание брони возвращает статус 202 Accepted и корректный заголовок Location.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_Returns202AndCorrectLocationHeader()
    {
        var ct = TestContext.Current.CancellationToken;

        var adminToken = await _factory.CreateUserAndGetTokenAsync("admin", "password", UserRole.Admin, ct);

        var eventsClient = _factory.CreateEventsClient();
        SetBearer(eventsClient, adminToken);

        var createEventRequest = new CreateEventRequest
        {
            Title = "Тестовое событие",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 5
        };

        var createEventResponse = await eventsClient.PostAsJsonAsync("/events", createEventRequest, ct);
        createEventResponse.EnsureSuccessStatusCode();

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(ct);
        Assert.NotNull(createdEvent);

        var bookingsClient = _factory.CreateBookingsClient();
        SetBearer(bookingsClient, adminToken);

        var bookResponse = await bookingsClient.PostAsync($"/bookings/{createdEvent.Id}", null, ct);

        Assert.Equal(HttpStatusCode.Accepted, bookResponse.StatusCode);

        var locationHeader = bookResponse.Headers.Location;
        Assert.NotNull(locationHeader);
        Assert.True(locationHeader.IsAbsoluteUri);
        Assert.Contains("/bookings/", locationHeader.AbsolutePath, StringComparison.OrdinalIgnoreCase);

        var booking = await bookResponse.Content.ReadFromJsonAsync<BookingResponse>(ct);
        Assert.NotNull(booking);
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    /// <summary>
    /// Проверяет, что попытка отмены брони другим пользователем возвращает статус 403 Forbidden.
    /// </summary>
    [Fact]
    public async Task CancelBookingAsync_NonOwnerAttemptsToCancelOthersBooking_Returns403Forbidden()
    {
        var ct = TestContext.Current.CancellationToken;

        var adminToken = await _factory.CreateUserAndGetTokenAsync("admin", "password", UserRole.Admin, ct);
        var firstUserToken = await _factory.CreateUserAndGetTokenAsync("user_first", "password", UserRole.User, ct);
        var secondUserToken = await _factory.CreateUserAndGetTokenAsync("user_second", "password", UserRole.User, ct);

        var eventsClient = _factory.CreateEventsClient();
        SetBearer(eventsClient, adminToken);

        var createEventRequest = new CreateEventRequest
        {
            Title = "Событие для проверки авторизации",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 5
        };

        var createEventResponse = await eventsClient.PostAsJsonAsync("/events", createEventRequest, ct);
        createEventResponse.EnsureSuccessStatusCode();

        var createdEvent = await createEventResponse.Content.ReadFromJsonAsync<EventResponse>(ct);
        Assert.NotNull(createdEvent);

        var firstUserBookingsClient = _factory.CreateBookingsClient();
        SetBearer(firstUserBookingsClient, firstUserToken);

        var createBookingResponse = await firstUserBookingsClient.PostAsync($"/bookings/{createdEvent.Id}", null, ct);
        createBookingResponse.EnsureSuccessStatusCode();

        var booking = await createBookingResponse.Content.ReadFromJsonAsync<BookingResponse>(ct);
        Assert.NotNull(booking);

        var secondUserBookingsClient = _factory.CreateBookingsClient();
        SetBearer(secondUserBookingsClient, secondUserToken);

        var cancelResponse = await secondUserBookingsClient.DeleteAsync($"/bookings/{booking.Id}", ct);

        Assert.Equal(HttpStatusCode.Forbidden, cancelResponse.StatusCode);
    }

    /// <summary>
    /// Проверяет, что попытка создания более 10 активных бронирований возвращает статус 409 Conflict.
    /// </summary>
    [Fact]
    public async Task CreateBookingAsync_WhenMoreThan10ActiveBookings_Returns409Conflict()
    {
        var ct = TestContext.Current.CancellationToken;

        var userToken = await _factory.CreateUserAndGetTokenAsync("limited_user", "password", UserRole.User, ct);
        var bookingsClient = _factory.CreateBookingsClient();
        SetBearer(bookingsClient, userToken);

        for (var i = 0; i < 10; i++)
        {
            var response = await bookingsClient.PostAsync($"/bookings/{Guid.NewGuid()}", null, ct);
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }

        var overflowResponse = await bookingsClient.PostAsync($"/bookings/{Guid.NewGuid()}", null, ct);

        Assert.Equal(HttpStatusCode.Conflict, overflowResponse.StatusCode);
    }
}
