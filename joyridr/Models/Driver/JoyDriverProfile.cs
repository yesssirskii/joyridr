namespace Joyridr.Models;

/// <summary>Driver-specific data. Shares its primary key with <see cref="User"/>.</summary>
public class JoyDriverProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string? Bio { get; set; }
    public string? City { get; set; }

    /// <summary>
    /// Where the driver (and their cars) are based. Used for the "nearby" search and as the
    /// meeting point when the rider comes to the driver. Only shown to a rider after the ride is confirmed.
    /// </summary>
    public double? BaseLatitude { get; set; }
    public double? BaseLongitude { get; set; }
    public string? BaseAddress { get; set; }

    /// <summary>How far the driver is willing to travel to pick a rider up.</summary>
    public int ServiceRadiusKm { get; set; } = 20;

    /// <summary>IANA time zone the weekly schedule is expressed in, e.g. "Europe/Zagreb".</summary>
    public string TimeZoneId { get; set; } = "Europe/Zagreb";

    public bool IsAcceptingRides { get; set; } = true;

    /// <summary>Set by an admin once the driver's licence/identity has been checked.</summary>
    public bool IsVerified { get; set; }

    public double RatingAverage { get; set; }
    public int RatingCount { get; set; }
    public int CompletedRides { get; set; }

    public List<Car> Cars { get; set; } = [];
    public List<DriverWeeklyAvailability> WeeklyAvailability { get; set; } = [];
    public List<DriverTimeOff> TimeOff { get; set; } = [];
}
