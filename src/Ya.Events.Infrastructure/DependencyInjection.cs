using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;
using Ya.Events.Application.Abstractions.Caching;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Infrastructure.Caching;
using Ya.Events.Infrastructure.Options;
using Ya.Events.Infrastructure.Persistence;
using Ya.Events.Infrastructure.Repositories;
using Ya.Events.Infrastructure.Services;

namespace Ya.Events.Infrastructure;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtOptions = configuration
            .GetSection("Jwt")
            .Get<JwtOptions>();

        if (jwtOptions is null)
        {
            throw new InvalidOperationException("JWT options are not configured.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,

                ValidateLifetime = true,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),

                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }

    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IEventRepository, EventRepository>();

        // Сначала инициализация топика, потом подписчик
        services.AddHostedService<KafkaTopicInitializer>();
        services.AddHostedService<BookingConsumerWorker>();

        // Redis connection multiplexer
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var redisOptions = configuration
                .GetSection(RedisOptions.SectionName)
                .Get<RedisOptions>();

            if (redisOptions is null)
            {
                throw new InvalidOperationException("Redis options are not configured.");
            }

            var options = new ConfigurationOptions
            {
                EndPoints = { redisOptions.ConnectionString },
                ConnectTimeout = 3000,
                AbortOnConnectFail = false,
            };

            return ConnectionMultiplexer.Connect(options);
        });

        services.AddSingleton<ICache, RedisCache>();
        services.AddSingleton<ICacheTtlProvider, RedisCacheTtlProvider>();

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
