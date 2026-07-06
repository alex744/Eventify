using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Security;
using Ya.Events.Infrastructure.Persistence;
using Ya.Events.Infrastructure.Repositories;
using Ya.Events.Infrastructure.Security;
using Ya.Events.Infrastructure.Services;

namespace Ya.Events.Infrastructure;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(options =>
            configuration.GetSection("Jwt"));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddHostedService<BookingProcessorService>();

        return services;
    }

    public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
    {
        using (var scope = app.ApplicationServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        return app;
    }
}
