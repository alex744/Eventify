using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Shared.Contracts.Events;
using Ya.Users.Application.DTOs;
using Ya.Users.Domain.ValueObjects;

using UsersEntryPoint = Ya.Users.Api.Controllers.AuthController;
using EventsEntryPoint = Ya.Events.Api.Controllers.EventsController;
using BookingsEntryPoint = Ya.Bookings.Api.Controllers.BookingsController;

namespace Ya.E2ETests.Fixtures;

public sealed class WebApiFactory : IAsyncLifetime, IAsyncDisposable
{
    private const string JwtSecret = "test-secret-key-for-e2e-tests-must-be-at-least-32-characters-long!";
    private const string JwtIssuer = "users-api";
    private const string JwtAudience = "events-api, bookings-api";

    private readonly PostgreSqlContainer _usersDb = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("users_e2e_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly PostgreSqlContainer _eventsDb = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("events_e2e_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly PostgreSqlContainer _bookingsDb = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("bookings_e2e_db")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private UsersApiFactory? _usersFactory;
    private EventsApiFactory? _eventsFactory;
    private BookingsApiFactory? _bookingsFactory;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(
            _usersDb.StartAsync(),
            _eventsDb.StartAsync(),
            _bookingsDb.StartAsync());

        _usersFactory = new UsersApiFactory(_usersDb.GetConnectionString());
        _eventsFactory = new EventsApiFactory(_eventsDb.GetConnectionString());
        _bookingsFactory = new BookingsApiFactory(_bookingsDb.GetConnectionString());

        // Явный прогрев всех трёх сервисов.
        _ = _usersFactory.CreateClient();
        _ = _eventsFactory.CreateClient();
        _ = _bookingsFactory.CreateClient();
    }

    public HttpClient CreateUsersClient()
        => (_usersFactory ?? throw new InvalidOperationException("Users factory is not initialized.")).CreateClient();

    public HttpClient CreateEventsClient()
        => (_eventsFactory ?? throw new InvalidOperationException("Events factory is not initialized.")).CreateClient();

    public HttpClient CreateBookingsClient()
        => (_bookingsFactory ?? throw new InvalidOperationException("Bookings factory is not initialized.")).CreateClient();

    public async Task<string> CreateUserAndGetTokenAsync(
        string loginPrefix,
        string password,
        UserRole role,
        CancellationToken ct = default)
    {
        var usersClient = CreateUsersClient();
        var login = $"{loginPrefix}_{Guid.NewGuid():N}";

        var registerRequest = new RegisterUserRequest
        {
            Login = login,
            Password = password,
            Role = role.ToString()
        };

        var registerResponse = await usersClient.PostAsJsonAsync("/auth/register", registerRequest, ct);
        if (registerResponse.StatusCode != HttpStatusCode.NoContent)
        {
            var details = await registerResponse.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Не удалось зарегистрировать пользователя '{login}'. Status={(int)registerResponse.StatusCode}. Body={details}");
        }

        var loginRequest = new LoginUserRequest
        {
            Login = login,
            Password = password
        };

        var loginResponse = await usersClient.PostAsJsonAsync("/auth/login", loginRequest, ct);
        loginResponse.EnsureSuccessStatusCode();

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
            throw new InvalidOperationException($"Не удалось получить JWT для пользователя '{login}'.");

        return auth.AccessToken;
    }

    public async ValueTask DisposeAsync()
    {
        if (_bookingsFactory is not null) await _bookingsFactory.DisposeAsync();
        if (_eventsFactory is not null) await _eventsFactory.DisposeAsync();
        if (_usersFactory is not null) await _usersFactory.DisposeAsync();

        await Task.WhenAll(
            _bookingsDb.DisposeAsync().AsTask(),
            _eventsDb.DisposeAsync().AsTask(),
            _usersDb.DisposeAsync().AsTask());
    }

    private static void ConfigureCommon(IWebHostBuilder builder, string connectionString)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:LifetimeMinutes", "60");

        // Конфиги Kafka для хостов, где есть интеграция.
        builder.UseSetting("Kafka:BootstrapServers", "localhost:9092");
        builder.UseSetting("Kafka:GroupId", "bookings-e2e");
        builder.UseSetting("Kafka:ConsumerGroup", "events-e2e");
    }

    private static void RemoveHostedServicesByTypeName(IServiceCollection services, params string[] typeNames)
    {
        var descriptors = services
            .Where(d => d.ServiceType == typeof(IHostedService)
                        && d.ImplementationType is not null
                        && typeNames.Contains(d.ImplementationType.Name, StringComparer.Ordinal))
            .ToList();

        foreach (var descriptor in descriptors)
            services.Remove(descriptor);
    }

    private sealed class UsersApiFactory(string connectionString) : WebApplicationFactory<UsersEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
            => ConfigureCommon(builder, connectionString);
    }

    private sealed class EventsApiFactory(string connectionString) : WebApplicationFactory<EventsEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ConfigureCommon(builder, connectionString);

            builder.ConfigureServices(services =>
            {
                // Для E2E HTTP-сценариев отключаем Kafka background workers.
                RemoveHostedServicesByTypeName(services, "KafkaTopicInitializer", "BookingConsumerWorker");
            });
        }
    }

    private sealed class BookingsApiFactory(string connectionString) : WebApplicationFactory<BookingsEntryPoint>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ConfigureCommon(builder, connectionString);

            builder.ConfigureServices(services =>
            {
                // Для E2E HTTP-сценариев отключаем фоновую Kafka-обработку.
                RemoveHostedServicesByTypeName(services, "BookingProcessorService");

                // И исключаем реальную публикацию в Kafka.
                services.AddSingleton<IBookingEventPublisher, NoOpBookingEventPublisher>();
            });
        }
    }

    private sealed class NoOpBookingEventPublisher : IBookingEventPublisher
    {
        public Task PublishAsync(BookingConfirmed message, CancellationToken ct = default) => Task.CompletedTask;
    }
}
