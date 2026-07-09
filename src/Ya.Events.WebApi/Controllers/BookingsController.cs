using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ya.Events.Application.Abstractions.Services;
using Ya.Events.Application.DTOs.Bookings;
using Ya.Events.Application.Mappers;
using Ya.Events.WebApi.Extensions;

namespace Ya.Events.WebApi.Controllers;

[ApiController]
[Route("[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    /// <summary>
    /// Возвращает текущее состояние брони по её идентификатору
    /// GET /bookings/{id}
    /// </summary>    
    [Authorize]
    [HttpGet("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BookingResponse))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<ActionResult<BookingResponse>> GetBookingAsync(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var booking = await _bookingService.GetBookingByIdAsync(id, ct);
        if (booking == null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"Бронь с идентификатором '{id}' не найдена."
            });

        return booking.ToResponse();
    }

    /// <summary>
    /// Отменить бронь
    /// DELETE /bookings/{id}
    /// </summary>    
    [Authorize]
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> CancelBookingAsync(Guid id, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var isAdmin = User.IsInRole("Admin");

        await _bookingService.CancelBookingAsync(id, userId, isAdmin, ct);
        return NoContent();
    }
}
