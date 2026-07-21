using Microsoft.Extensions.DependencyInjection;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Bookings.Application.Services;

namespace Ya.Bookings.Application;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}
