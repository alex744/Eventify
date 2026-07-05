using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ya.Events.Domain.Entities;

namespace Ya.Events.Infrastructure.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(u => u.Login)
            .HasColumnName("login")
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(u => u.Login)
            .IsUnique();

        builder.HasMany(u => u.Bookings)
            .WithOne(b => b.User)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}