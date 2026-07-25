using Ya.Users.Domain.ValueObjects;

namespace Ya.Users.Application.Abstractions.Security;

public interface IAccessTokenGenerator
{
    string CreateToken(Guid userId, string login, UserRole role);
}
