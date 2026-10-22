using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class DriverWeeklyAvailabilityConfiguration : IEntityTypeConfiguration<DriverWeeklyAvailability>
{
    public void Configure(EntityTypeBuilder<DriverWeeklyAvailability> b)
    {
        b.ToTable("driver_weekly_availability", t => t.HasCheckConstraint("ck_driver_weekly_availability_times", "end_time > start_time"));
        b.HasOne(a => a.Driver).WithMany(d => d.WeeklyAvailability).HasForeignKey(a => a.DriverId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(a => new { a.DriverId, a.DayOfWeek });
    }
}

public class DriverTimeOffConfiguration : IEntityTypeConfiguration<DriverTimeOff>
{
    public void Configure(EntityTypeBuilder<DriverTimeOff> b)
    {
        b.ToTable("driver_time_off", t => t.HasCheckConstraint("ck_driver_time_off_times", "ends_at > starts_at"));
        b.Property(t => t.Reason).HasMaxLength(200);
        b.HasOne(t => t.Driver).WithMany(d => d.TimeOff).HasForeignKey(t => t.DriverId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(t => new { t.DriverId, t.StartsAt });
    }
}
