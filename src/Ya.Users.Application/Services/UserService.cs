using System.ComponentModel.DataAnnotations;
using Ya.Users.Application.Abstractions.Persistence.Repositories;
using Ya.Users.Application.Abstractions.Security;
using Ya.Users.Application.Abstractions.Services;
using Ya.Users.Application.DTOs;
using Ya.Users.Domain.Entities;
using Ya.Users.Domain.Exceptions;
using Ya.Users.Domain.ValueObjects;

namespace Ya.Users.Application.Services;

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
            throw new NotFoundException("Неверный логин или пароль.");

        var user = await _repository.GetByLoginAsync(request.Login, ct);
        if (user is null)
            throw new NotFoundException("Неверный логин или пароль.");

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new NotFoundException("Неверный логин или пароль.");

        var token = _tokenGenerator.CreateToken(user.Id, user.Login, user.Role);
        return new AuthResponse(user.Id, user.Login, user.Role.ToString(), token);
    }
}
