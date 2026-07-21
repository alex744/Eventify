using Microsoft.EntityFrameworkCore;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<Event> Events => Set<Event>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
