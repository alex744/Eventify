namespace Ya.Events.Application.Abstractions.Security;

public interface IAccessTokenGenerator
{
    string CreateToken(Guid userId, string login, string role);
}
