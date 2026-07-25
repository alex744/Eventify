namespace Ya.Users.Infrastructure.Security;

public sealed class JwtOptions
{
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int LifetimeMinutes { get; init; } = 60;
}
