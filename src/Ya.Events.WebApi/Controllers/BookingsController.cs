using Microsoft.AspNetCore.Mvc;
using Ya.Events.WebApi.DTOs.Responses;
using Ya.Events.WebApi.Interfaces;
using Ya.Events.WebApi.Mappings;

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
    [HttpGet("{id}")]
    public async Task<ActionResult<BookingResponse>> GetBookingAsync(Guid id, CancellationToken ct = default)
    {
        var booking = await _bookingService.GetBookingByIdAsync(id, ct);
        if (booking == null)
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Detail = $"Бронь с идентификатором '{id}' не найдена."
            });

        return booking.ToResponse();
    }
}
