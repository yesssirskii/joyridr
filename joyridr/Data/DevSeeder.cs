using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Data;

/// <summary>
/// Fills an empty development database with demo accounts and cars so the iOS app has
/// something to show. All accounts use the password <see cref="Password"/>.
/// </summary>
public static class DevSeeder
{
    public const string Password = "Password123!";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher, ILogger logger)
    {
        if (await db.Users.AnyAsync())
            return;

        var admin = NewUser("admin@joyridr.dev", "Admin", "Joyridr", UserRole.Admin, hasher);

        var rider = NewUser("rider@joyridr.dev", "Luka", "Horvat", UserRole.JoyRider, hasher);
        rider.PhoneNumber = "+385911111111";
        rider.RiderProfile = new JoyRiderProfile { DateOfBirth = new DateOnly(1998, 5, 14) };

        var ivan = NewUser("driver@joyridr.dev", "Ivan", "Kovač", UserRole.JoyDriver, hasher);
        ivan.PhoneNumber = "+385922222222";
        ivan.DriverProfile = new JoyDriverProfile
        {
            Bio = "Track day regular. I'll show you what these cars can really do (legally).",
            City = "Zagreb",
            BaseLatitude = 45.8131,
            BaseLongitude = 15.9775,
            BaseAddress = "Trg bana Josipa Jelačića, Zagreb",
            ServiceRadiusKm = 25,
            IsVerified = true,
            WeeklyAvailability = EveryDay(new TimeOnly(10, 0), new TimeOnly(20, 0)),
            Cars =
            [
                NewCar("Porsche", "911 GT3", 2023, "Shark Blue", CarCategory.SportsCar, Transmission.Manual, 510, 3.4m, 318, 1, 180m, CarStatus.Approved),
                NewCar("Ferrari", "296 GTB", 2024, "Rosso Corsa", CarCategory.Supercar, Transmission.Automatic, 830, 2.9m, 330, 1, 350m, CarStatus.Approved),
                NewCar("Lamborghini", "Huracán STO", 2022, "Verde Mantis", CarCategory.Supercar, Transmission.Automatic, 640, 3.0m, 310, 1, 320m, CarStatus.PendingReview),
            ],
        };

        var marko = NewUser("driver2@joyridr.dev", "Marko", "Babić", UserRole.JoyDriver, hasher);
        marko.DriverProfile = new JoyDriverProfile
        {
            Bio = "Weekend JDM legend tours around Zagreb and Samobor.",
            City = "Zagreb",
            BaseLatitude = 45.7930,
            BaseLongitude = 15.9600,
            BaseAddress = "Bundek, Zagreb",
            ServiceRadiusKm = 15,
            WeeklyAvailability =
            [
                new DriverWeeklyAvailability { DayOfWeek = DayOfWeek.Friday, StartTime = new TimeOnly(16, 0), EndTime = new TimeOnly(22, 0) },
                new DriverWeeklyAvailability { DayOfWeek = DayOfWeek.Saturday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(21, 0) },
                new DriverWeeklyAvailability { DayOfWeek = DayOfWeek.Sunday, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(18, 0) },
            ],
            Cars =
            [
                NewCar("Nissan", "GT-R Nismo", 2020, "Pearl White", CarCategory.SportsCar, Transmission.Automatic, 600, 2.8m, 315, 2, 150m, CarStatus.Approved),
            ],
        };

        db.Users.AddRange(admin, rider, ivan, marko);
        await db.SaveChangesAsync();
        logger.LogInformation("Seeded development data. Log in as rider@joyridr.dev, driver@joyridr.dev or admin@joyridr.dev with password {Password}", Password);
    }

    private static User NewUser(string email, string firstName, string lastName, UserRole role, IPasswordHasher<User> hasher)
    {
        var user = new User { Email = email, FirstName = firstName, LastName = lastName, Role = role };
        user.PasswordHash = hasher.HashPassword(user, Password);
        return user;
    }

    private static Car NewCar(string make, string model, int year, string color, CarCategory category, Transmission transmission,
        int hp, decimal zeroToHundred, int topSpeed, int seats, decimal pricePerHour, CarStatus status) => new()
    {
        Make = make,
        Model = model,
        Year = year,
        Color = color,
        Category = category,
        Transmission = transmission,
        Horsepower = hp,
        ZeroToHundredSeconds = zeroToHundred,
        TopSpeedKmh = topSpeed,
        PassengerSeats = seats,
        PricePerHour = pricePerHour,
        Status = status,
        Description = $"{year} {make} {model} in {color}.",
    };

    private static List<DriverWeeklyAvailability> EveryDay(TimeOnly start, TimeOnly end) =>
        Enum.GetValues<DayOfWeek>().Select(d => new DriverWeeklyAvailability { DayOfWeek = d, StartTime = start, EndTime = end }).ToList();
}
