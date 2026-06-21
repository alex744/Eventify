using Ya.Events.WebApi.Middleware;

namespace Ya.Events.WebApi;

public static class DependencyInjectionExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
    }
}
