using System.ComponentModel.DataAnnotations;

namespace Ya.Events.Application.DTOs.Auth;

public record LoginUserRequest
{
    [Required(ErrorMessage = "Логин обязателен.")]
    [MinLength(3, ErrorMessage = "Логин должен содержать не менее 3 символов.")]
    [MaxLength(256, ErrorMessage = "Логин не должен превышать 256 символов.")]
    public required string Login { get; init; }

    [Required(ErrorMessage = "Пароль обязателен.")]
    [MinLength(6, ErrorMessage = "Пароль должен содержать не менее 6 символов.")]
    public required string Password { get; init; }
}
