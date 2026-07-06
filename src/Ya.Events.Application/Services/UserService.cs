using System.ComponentModel.DataAnnotations;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Application.Abstractions.Security;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs.Auth;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.ValueObjects;

namespace Ya.Events.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAccessTokenGenerator _tokenGenerator;

    public UserService(
        IUserRepository repository,
        IPasswordHasher passwordHasher,
        IAccessTokenGenerator tokenGenerator)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task RegisterAsync(RegisterUserRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Логин и пароль обязательны.");

        if (!Enum.TryParse<UserRole>(request.Role ?? "User", ignoreCase: false, out var role))
            throw new ArgumentException("Некорректная роль пользователя.");

        var exists = await _repository.GetByLoginAsync(request.Login, ct);
        if (exists is not null)
            throw new ValidationException("Пользователь с таким логином уже существует.");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.Login, passwordHash, role);

        await _repository.CreateAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginUserRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password))
            throw new ValidationException("Логин и пароль обязательны.");

        var user = await _repository.GetByLoginAsync(request.Login, ct);
        if (user is null)
            throw new ValidationException("Пользователь не найден.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new ValidationException("Неверный логин или пароль.");

        var token = _tokenGenerator.CreateToken(user.Id, user.Login, user.Role);
        return new AuthResponse(user.Id, user.Login, user.Role.ToString(), token);
    }
}
