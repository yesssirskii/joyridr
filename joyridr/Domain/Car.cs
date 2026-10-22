namespace Joyridr.Domain;

public class Car
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid DriverId { get; set; }
    public JoyDriverProfile Driver { get; set; } = null!;

    public string Make { get; set; } = null!;
    public string Model { get; set; } = null!;
    public int Year { get; set; }
    public string Color { get; set; } = null!;
    public CarCategory Category { get; set; }
    public Transmission Transmission { get; set; }
    public int Horsepower { get; set; }
    public decimal? ZeroToHundredSeconds { get; set; }
    public int? TopSpeedKmh { get; set; }

    /// <summary>Seats available to riders (excluding the driver).</summary>
    public int PassengerSeats { get; set; } = 1;
    public string? Description { get; set; }

    /// <summary>Private, only visible to the owner and admins.</summary>
    public string? LicensePlate { get; set; }

    public decimal PricePerHour { get; set; }
    public string Currency { get; set; } = "EUR";

    public CarStatus Status { get; set; } = CarStatus.PendingReview;
    public string? RejectionReason { get; set; }

    /// <summary>Driver can hide a car from search without deleting it.</summary>
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<CarPhoto> Photos { get; set; } = [];

    public bool IsListed => Status == CarStatus.Approved && IsActive && DeletedAt is null;
}
