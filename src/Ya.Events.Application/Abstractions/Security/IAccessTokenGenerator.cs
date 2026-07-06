using Ya.Events.Domain.ValueObjects;

namespace Ya.Events.Application.Abstractions.Security;

public interface IAccessTokenGenerator
{
    string CreateToken(Guid userId, string login, UserRole role);
}
