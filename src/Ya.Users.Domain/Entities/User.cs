using Ya.Users.Domain.ValueObjects;

namespace Ya.Users.Domain.Entities;

/// <summary>
/// Пользователь системы.
/// </summary>
public sealed class User
{
    /// <summary>
    /// Уникальный идентификатор пользователя.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Логин пользователя.
    /// </summary>
    public string Login { get; private set; }

    /// <summary>
    /// Хэш пароля пользователя.
    /// </summary>
    public string PasswordHash { get; private set; }

    /// <summary>
    /// Роль пользователя в системе.
    /// </summary>
    public UserRole Role { get; private set; }

    private User()
    {
        Login = null!;
        PasswordHash = null!;
    }

    private User(Guid id, string login, string passwordHash, UserRole role)
    {
        Id = id;
        Login = login;
        PasswordHash = passwordHash;
        Role = role;
    }

    /// <summary>
    /// Создаёт нового пользователя.
    /// </summary>
    /// <param name="login">Логин пользователя.</param>
    /// <param name="passwordHash">Хэш пароля пользователя.</param>
    /// <param name="role">Роль пользователя в системе.</param>
    /// <returns>Новый пользователь.</returns>
    public static User Create(string login, string passwordHash, UserRole role)
    {
        ThrowIfNotValid(login, passwordHash);

        return new User(Guid.NewGuid(), login.Trim(), passwordHash, role);
    }

    private static void ThrowIfNotValid(string login, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(login))
            throw new ArgumentException("Логин не может быть пустым.", nameof(Login));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("Хэш пароля не может быть пустым.", nameof(PasswordHash));
    }
}
