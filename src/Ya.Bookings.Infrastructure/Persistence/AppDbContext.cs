using Microsoft.EntityFrameworkCore;
using Ya.Bookings.Domain.Entities;

namespace Ya.Bookings.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<Booking> Bookings => Set<Booking>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
