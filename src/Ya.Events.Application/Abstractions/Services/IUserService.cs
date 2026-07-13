using Ya.Events.Application.DTOs.Auth;

namespace Ya.Events.Application.Abstractions.Services;

public interface IUserService
{
    Task RegisterAsync(RegisterUserRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginUserRequest request, CancellationToken ct = default);
}
