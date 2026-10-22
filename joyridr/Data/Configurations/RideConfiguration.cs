using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> b)
    {
        // The "no two active rides for the same driver at the same time" exclusion constraint
        // can't be expressed in EF's model, it lives in the AddRideOverlapConstraint migration.
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_rides_times", "ends_at > starts_at");
            t.HasCheckConstraint("ck_rides_price", "price >= 0");
        });

        b.Property(r => r.Version).IsRowVersion();
        b.Property(r => r.MeetingAddress).HasMaxLength(300);
        b.Property(r => r.RiderNote).HasMaxLength(1000);
        b.Property(r => r.DeclineReason).HasMaxLength(500);
        b.Property(r => r.CancellationReason).HasMaxLength(500);
        b.Property(r => r.Currency).HasMaxLength(3).IsFixedLength();

        b.HasOne(r => r.Rider).WithMany().HasForeignKey(r => r.RiderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Driver).WithMany().HasForeignKey(r => r.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Car).WithMany().HasForeignKey(r => r.CarId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(r => new { r.RiderId, r.StartsAt });
        b.HasIndex(r => new { r.DriverId, r.StartsAt });
        b.HasIndex(r => new { r.Status, r.RespondBy });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> b)
    {
        b.Property(p => p.Provider).HasMaxLength(32);
        b.Property(p => p.ProviderPaymentId).HasMaxLength(128);
        b.Property(p => p.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(p => p.FailureReason).HasMaxLength(500);
        b.HasOne(p => p.Ride).WithOne(r => r.Payment).HasForeignKey<Payment>(p => p.RideId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(p => p.RideId).IsUnique();
        b.HasIndex(p => p.ProviderPaymentId);
    }
}

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> b)
    {
        b.ToTable(t => t.HasCheckConstraint("ck_reviews_rating", "rating BETWEEN 1 AND 5"));
        b.Property(r => r.Comment).HasMaxLength(1000);
        b.HasOne(r => r.Ride).WithMany(r => r.Reviews).HasForeignKey(r => r.RideId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(r => r.Author).WithMany().HasForeignKey(r => r.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(r => r.Subject).WithMany().HasForeignKey(r => r.SubjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(r => new { r.RideId, r.AuthorId }).IsUnique();
        b.HasIndex(r => new { r.SubjectId, r.CreatedAt });
    }
}
