using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs.Auth;

namespace Ya.Events.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService _userService;

    public AuthController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Регистрирует нового пользователя в системе.
    /// POST /auth/register
    /// </summary>    
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> Register([FromBody] RegisterUserRequest request, CancellationToken ct = default)
    {
        await _userService.RegisterAsync(request, ct);
        return NoContent();
    }

    /// <summary>
    /// Авторизует пользователя в системе и возвращает токен доступа.
    /// POST /auth/login
    /// </summary>    
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(AuthResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginUserRequest request, CancellationToken ct = default)
    {
        var result = await _userService.LoginAsync(request, ct);
        return Ok(result);
    }
}
