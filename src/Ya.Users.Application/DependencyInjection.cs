using Microsoft.Extensions.DependencyInjection;
using Ya.Users.Application.Abstractions.Services;
using Ya.Users.Application.Services;

namespace Ya.Users.Application;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {        
        services.AddScoped<IUserService, UserService>();

        return services;
    }
}
