using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider clock) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<JoyRiderProfile> RiderProfiles => Set<JoyRiderProfile>();
    public DbSet<JoyDriverProfile> DriverProfiles => Set<JoyDriverProfile>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Car> Cars => Set<Car>();
    public DbSet<CarPhoto> CarPhotos => Set<CarPhoto>();
    public DbSet<DriverWeeklyAvailability> DriverWeeklyAvailability => Set<DriverWeeklyAvailability>();
    public DbSet<DriverTimeOff> DriverTimeOff => Set<DriverTimeOff>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed for the "no overlapping rides per driver" exclusion constraint.
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable strings instead of ints.
        configurationBuilder.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<CarCategory>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<CarStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<Transmission>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<RideStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<MeetingPointType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(32);
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        SetTimestamps();
        return base.SaveChanges();
    }

    /// <summary>Fills CreatedAt/UpdatedAt on any entity that has them.</summary>
    private void SetTimestamps()
    {
        var now = clock.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added
                && entry.Metadata.FindProperty("CreatedAt") is not null
                && entry.Property("CreatedAt").CurrentValue is DateTimeOffset created
                && created == default)
            {
                entry.Property("CreatedAt").CurrentValue = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified
                && entry.Metadata.FindProperty("UpdatedAt") is not null)
            {
                entry.Property("UpdatedAt").CurrentValue = now;
            }
        }
    }
}
