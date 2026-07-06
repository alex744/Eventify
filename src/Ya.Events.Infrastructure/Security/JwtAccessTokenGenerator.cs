using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Ya.Events.Application.Abstractions.Security;

namespace Ya.Events.Infrastructure.Security;

public sealed class JwtAccessTokenGenerator : IAccessTokenGenerator
{
    private readonly JwtOptions _jwtOptions;

    public JwtAccessTokenGenerator(IOptions<JwtOptions> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);

        _jwtOptions = jwtOptions.Value;
    }

    public string CreateToken(Guid userId, string login, string role)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId не может быть пустым.", nameof(userId));

        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Login не может быть пустым.", nameof(login));

        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role не может быть пустым.", nameof(role));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new("role", role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwtOptions.Issuer,
            Audience = _jwtOptions.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwtOptions.LifetimeMinutes),
            SigningCredentials = creds
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
