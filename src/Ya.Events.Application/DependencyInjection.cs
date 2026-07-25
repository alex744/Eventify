using Microsoft.Extensions.DependencyInjection;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.Services;

namespace Ya.Events.Application;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();

        return services;
    }
}
