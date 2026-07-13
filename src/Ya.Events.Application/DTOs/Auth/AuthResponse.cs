namespace Ya.Events.Application.DTOs.Auth;

public record AuthResponse(Guid UserId, string Login, string Role, string AccessToken);