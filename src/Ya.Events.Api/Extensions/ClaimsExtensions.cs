using System.Security.Claims;

namespace Ya.Events.Api.Extensions;

/// <summary>
/// Вспомогательные методы для работы с claims пользователя.
/// </summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Получает идентификатор пользователя из claims.
    /// </summary>
    /// <param name="principal">Объект ClaimsPrincipal содержащий claims пользователя.</param>
    /// <returns>GUID пользователя.</returns>
    /// <exception cref="UnauthorizedAccessException">Выбрасывается, если userId не найден в claims или имеет неверный формат.</exception>
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("sub")
            ?? principal.FindFirst(ClaimTypes.Sid);

        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
            throw new UnauthorizedAccessException("Неверный токен аутентификации.");

        return userId;
    }
}
