using Joyridr.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Joyridr.Data.Configurations;

public class CarConfiguration : IEntityTypeConfiguration<Car>
{
    public void Configure(EntityTypeBuilder<Car> b)
    {
        b.ToTable(t =>
        {
            t.HasCheckConstraint("ck_cars_price_positive", "price_per_hour > 0");
            t.HasCheckConstraint("ck_cars_seats", "passenger_seats BETWEEN 1 AND 8");
            t.HasCheckConstraint("ck_cars_horsepower", "horsepower > 0");
        });

        b.Property(c => c.Make).HasMaxLength(60);
        b.Property(c => c.Model).HasMaxLength(100);
        b.Property(c => c.Color).HasMaxLength(40);
        b.Property(c => c.Description).HasMaxLength(2000);
        b.Property(c => c.LicensePlate).HasMaxLength(20);
        b.Property(c => c.Currency).HasMaxLength(3).IsFixedLength();
        b.Property(c => c.RejectionReason).HasMaxLength(500);
        b.Property(c => c.ZeroToHundredSeconds).HasPrecision(4, 2);
        b.Ignore(c => c.IsListed);

        b.HasOne(c => c.Driver).WithMany(d => d.Cars).HasForeignKey(c => c.DriverId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.Status, c.IsActive });
    }
}

public class CarPhotoConfiguration : IEntityTypeConfiguration<CarPhoto>
{
    public void Configure(EntityTypeBuilder<CarPhoto> b)
    {
        b.Property(p => p.Url).HasMaxLength(512);
        b.HasOne(p => p.Car).WithMany(c => c.Photos).HasForeignKey(p => p.CarId).OnDelete(DeleteBehavior.Cascade);
    }
}
