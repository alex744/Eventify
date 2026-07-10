using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Ya.Events.Application.Abstractions.Security;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.Infrastructure.Persistence;

namespace Ya.Events.WebApi.IntegrationTests.Fixtures;

public class WebApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("testdb")
        .Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Удаляем существующий DbContext, зарегистрированный в Program.cs
            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Регистрируем DbContext с PostgreSQL контейнером для тестов
            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_postgres.GetConnectionString()));
        });

        base.ConfigureWebHost(builder);
    }

    public async Task<string> CreateUserAndGetTokenAsync(
        string login,
        string password,
        UserRole role,
        CancellationToken ct = default)
    {
        using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var tokenGenerator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();

        // Проверяем, существует ли уже такой пользователь
        var existingUser = await dbContext.Users.FirstOrDefaultAsync(u => u.Login == login, ct);
        if (existingUser is not null)
        {
            // Генерируем и возвращаем токен для существующего пользователя
            return tokenGenerator.CreateToken(existingUser.Id, existingUser.Login, existingUser.Role);
        }

        // Создаём нового пользователя
        var passwordHash = passwordHasher.Hash(password);
        var user = User.Create(login, passwordHash, role);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(ct);

        // Генерируем и возвращаем JWT токен
        return tokenGenerator.CreateToken(user.Id, user.Login, user.Role);
    }
}
