using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Infrastructure.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(b => b.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(b => b.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(b => b.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(b => b.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(b => b.ProcessedAt)
            .HasColumnName("processed_at");

        builder.HasOne(b => b.Event)
            .WithMany(e => e.Bookings)
            .HasForeignKey(b => b.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.User)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
