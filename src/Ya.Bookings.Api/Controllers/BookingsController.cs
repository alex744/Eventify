using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ya.Bookings.Api.Extensions;
using Ya.Bookings.Application.Abstractions.Services;
using Ya.Bookings.Application.DTOs;
using Ya.Bookings.Application.Mappers;

namespace Ya.Bookings.Api.Controllers;

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
        var booking = await _bookingService.GetBookingByIdAsync(id, userId, ct);
        if (booking == null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"Бронь с идентификатором '{id}' не найдена."
            });

        return booking.ToResponse();
    }

    /// <summary>
    /// Cоздать бронь на событие
    /// POST /bookings/{eventId}
    /// </summary>
    /// <param name="eventId">Идентификатор события.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Бронь со статусом Pending; код 202 Accepted с Location в заголовке.</returns>
    /// <response code="202">Бронь успешно создана и находится в ожидании подтверждения.</response>
    /// <response code="400">Событие уже началось.</response>
    /// <response code="401">Требуется аутентификация.</response>
    /// <response code="404">Событие с указанным идентификатором не найдено.</response>
    /// <response code="409">Нет доступных мест для бронирования.</response>
    [Authorize]
    [HttpPost("{eventId}")]
    [ProducesResponseType(StatusCodes.Status202Accepted, Type = typeof(BookingResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status409Conflict, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> CreateBookingAsync(Guid eventId, CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var booking = await _bookingService.CreateBookingAsync(eventId, userId, ct);
        return AcceptedAtAction("GetBooking", new { id = booking.Id }, booking.ToResponse());
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
