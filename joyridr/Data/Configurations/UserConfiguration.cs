using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.Property(u => u.Email).HasMaxLength(256);
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.PasswordHash).HasMaxLength(512);
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.PhoneNumber).HasMaxLength(32);
        b.Property(u => u.AvatarUrl).HasMaxLength(512);

        b.HasOne(u => u.RiderProfile).WithOne(p => p.User)
            .HasForeignKey<JoyRiderProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(u => u.DriverProfile).WithOne(p => p.User)
            .HasForeignKey<JoyDriverProfile>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class JoyRiderProfileConfiguration : IEntityTypeConfiguration<JoyRiderProfile>
{
    public void Configure(EntityTypeBuilder<JoyRiderProfile> b)
    {
        b.ToTable("joy_riders");
        b.HasKey(p => p.UserId);
    }
}

public class JoyDriverProfileConfiguration : IEntityTypeConfiguration<JoyDriverProfile>
{
    public void Configure(EntityTypeBuilder<JoyDriverProfile> b)
    {
        b.ToTable("joy_drivers", t => t.HasCheckConstraint("ck_joy_drivers_service_radius", "service_radius_km BETWEEN 0 AND 500"));
        b.HasKey(p => p.UserId);
        b.Property(p => p.Bio).HasMaxLength(2000);
        b.Property(p => p.City).HasMaxLength(100);
        b.Property(p => p.BaseAddress).HasMaxLength(300);
        b.Property(p => p.TimeZoneId).HasMaxLength(64);
        // Speeds up the bounding-box part of the nearby search.
        b.HasIndex(p => new { p.BaseLatitude, p.BaseLongitude });
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.Property(t => t.TokenHash).HasMaxLength(64);
        b.HasIndex(t => t.TokenHash).IsUnique();
        b.HasIndex(t => t.FamilyId);
        b.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
