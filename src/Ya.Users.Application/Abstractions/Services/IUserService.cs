using Ya.Users.Application.DTOs;

namespace Ya.Users.Application.Abstractions.Services;

public interface IUserService
{
    Task RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginUserRequest request, CancellationToken ct = default);
}
