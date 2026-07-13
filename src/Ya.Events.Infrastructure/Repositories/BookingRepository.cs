using Microsoft.EntityFrameworkCore;
using Ya.Events.Application.Abstractions.Persistence.Repositories;
using Ya.Events.Domain.Entities;
using Ya.Events.Domain.ValueObjects;
using Ya.Events.Infrastructure.Persistence;

namespace Ya.Events.Infrastructure.Repositories;

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

    public async Task<int> CountActiveBookingsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Bookings
            .Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
            .CountAsync(b => b.UserId == userId, ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
