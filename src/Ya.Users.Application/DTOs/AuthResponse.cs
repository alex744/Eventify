namespace Ya.Users.Application.DTOs;

public record AuthResponse(Guid UserId, string Login, string Role, string AccessToken);