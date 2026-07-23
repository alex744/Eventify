using Ya.Shared.Contracts.Events;

namespace Ya.Bookings.Application.Abstractions.Services;

public interface IBookingEventPublisher
{
    Task PublishAsync(BookingConfirmed message, CancellationToken ct = default);
}
