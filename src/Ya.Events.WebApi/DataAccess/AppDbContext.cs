using Microsoft.EntityFrameworkCore;
using Ya.Events.WebApi.Models;

namespace Ya.Events.WebApi.DataAccess;

public sealed class AppDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Booking> Bookings => Set<Booking>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
