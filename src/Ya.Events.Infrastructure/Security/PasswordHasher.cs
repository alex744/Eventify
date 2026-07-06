using System.Security.Cryptography;
using System.Text;
using Ya.Events.Application.Abstractions.Security;

namespace Ya.Events.Infrastructure.Security;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Пароль не может быть пустым", nameof(password));

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hash))
            return false;

        var passwordHash = Hash(password);
        return string.Equals(passwordHash, hash, StringComparison.OrdinalIgnoreCase);
    }
}
