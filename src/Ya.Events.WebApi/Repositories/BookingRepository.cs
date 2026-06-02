using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.DataAccess;
using Ya.Events.WebApi.Enums;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _context;

    public BookingRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Booking?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct);
    }

    public async Task<Booking> CreateAsync(Booking entity, CancellationToken ct = default)
    {
        await _context.Bookings.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<Event?> GetEventByIdAsync(Guid eventId, CancellationToken ct = default)
    {
        return await _context.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct);
    }

    public async Task<IReadOnlyList<Guid>> GetPendingBookingIdsAsync(CancellationToken ct = default)
    {
        return await _context.Bookings
            .Where(b => b.Status == BookingStatus.Pending)
            .Select(b => b.Id)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
