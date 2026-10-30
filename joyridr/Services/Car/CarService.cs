using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Interfaces;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

public class CarService(AppDbContext db, IFileStorage storage, AvailabilityService availability, TimeProvider clock)
{
    private const int MaxCarsPerDriver = 10;
    private const int MaxPhotosPerCar = 12;

    /// <summary>Listed cars whose driver is based within the radius, closest first.</summary>
    public async Task<PagedResult<NearbyCarDto>> SearchNearbyAsync(NearbyCarsQuery q, CancellationToken ct)
    {
        var radiusKm = Math.Clamp(q.RadiusKm, 1, 200);
        var page = Math.Max(q.Page, 1);
        var pageSize = Math.Clamp(q.PageSize, 1, 50);
        var (minLat, maxLat, minLng, maxLng) = Geo.BoundingBox(q.Latitude, q.Longitude, radiusKm);

        var query = db.Cars
            .Where(c => c.Status == CarStatus.Approved && c.IsActive && c.DeletedAt == null)
            .Where(c => c.Driver.IsAcceptingRides
                        && c.Driver.BaseLatitude >= minLat && c.Driver.BaseLatitude <= maxLat
                        && c.Driver.BaseLongitude >= minLng && c.Driver.BaseLongitude <= maxLng);
        if (q.Category is not null)
            query = query.Where(c => c.Category == q.Category);
        if (q.MinSeats is not null)
            query = query.Where(c => c.PassengerSeats >= q.MinSeats);
        if (q.MaxPricePerHour is not null)
            query = query.Where(c => c.PricePerHour <= q.MaxPricePerHour);

        // The bounding box narrows things down in SQL; exact distance and sorting happen here.
        // Good for an MVP; switch to PostGIS once there are thousands of cars.
        var candidates = await query
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .Include(c => c.Photos)
            .AsSplitQuery()
            .ToListAsync(ct);

        var results = candidates
            .Select(c => (Car: c, Distance: Geo.DistanceKm(q.Latitude, q.Longitude, c.Driver.BaseLatitude!.Value, c.Driver.BaseLongitude!.Value)))
            .Where(x => x.Distance <= radiusKm)
            .OrderBy(x => x.Distance)
            .ToList();

        var pageResults = results.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var driverAvailability = await availability.GetAvailabilityAsync(pageResults.Select(x => x.Car.Driver).ToList(), ct);

        var items = pageResults
            .Select(x => new NearbyCarDto(
                CarSummaryDto.From(x.Car),
                DriverSummaryDto.From(x.Car.Driver),
                Math.Round(x.Distance, 1),
                Math.Round(x.Car.Driver.BaseLatitude!.Value, 2),
                Math.Round(x.Car.Driver.BaseLongitude!.Value, 2),
                driverAvailability[x.Car.DriverId].IsAvailableToday,
                driverAvailability[x.Car.DriverId].NextAvailableAt))
            .ToList();

        return new PagedResult<NearbyCarDto>(items, page, pageSize, results.Count);
    }

    public async Task<CarDetailDto> GetAsync(Guid carId, Guid viewerId, UserRole viewerRole, CancellationToken ct)
    {
        var car = await LoadCarAsync(carId, ct);
        var isOwner = car.DriverId == viewerId;
        if (!car.IsListed && !isOwner && viewerRole != UserRole.Admin)
            throw ApiException.NotFound("Car");
        return CarDetailDto.From(car, isOwner);
    }

    /// <summary>All of a driver's cars, including ones pending review.</summary>
    public async Task<List<CarDetailDto>> GetDriverCarsAsync(Guid driverId, CancellationToken ct)
    {
        var cars = await db.Cars
            .Include(c => c.Photos)
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .Where(c => c.DriverId == driverId && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .AsSplitQuery()
            .ToListAsync(ct);
        return cars.Select(c => CarDetailDto.From(c, isOwner: true)).ToList();
    }

    public async Task<CarDetailDto> CreateAsync(Guid driverId, UpsertCarRequest request, CancellationToken ct)
    {
        if (await db.Cars.CountAsync(c => c.DriverId == driverId && c.DeletedAt == null, ct) >= MaxCarsPerDriver)
            throw ApiException.BadRequest($"You can have at most {MaxCarsPerDriver} cars.", "too_many_cars");

        var car = new Car { DriverId = driverId };
        Apply(car, request);
        db.Cars.Add(car);
        await db.SaveChangesAsync(ct);
        return CarDetailDto.From(await LoadCarAsync(car.Id, ct), isOwner: true);
    }

    public async Task<CarDetailDto> UpdateAsync(Guid driverId, Guid carId, UpsertCarRequest request, CancellationToken ct)
    {
        var car = await LoadOwnCarAsync(driverId, carId, ct);

        // Changing what the car actually is needs a fresh review, so nobody can get a
        // supercar approved and then swap it for a hatchback.
        var identityChanged = car.Make != request.Make.Trim() || car.Model != request.Model.Trim()
                              || car.Year != request.Year || car.Category != request.Category;
        Apply(car, request);
        if (identityChanged && car.Status != CarStatus.PendingReview)
        {
            car.Status = CarStatus.PendingReview;
            car.RejectionReason = null;
        }

        await db.SaveChangesAsync(ct);
        return CarDetailDto.From(car, isOwner: true);
    }

    public async Task DeleteAsync(Guid driverId, Guid carId, CancellationToken ct)
    {
        var car = await LoadOwnCarAsync(driverId, carId, ct);
        var hasUpcomingRides = await db.Rides.AnyAsync(r => r.CarId == carId && Ride.BlockingStatuses.Contains(r.Status), ct);
        if (hasUpcomingRides)
            throw ApiException.Conflict("This car has upcoming rides. Cancel or finish them first.", "car_has_rides");

        // Soft delete: past rides still point at this car.
        car.DeletedAt = clock.GetUtcNow();
        car.IsActive = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CarPhotoDto> AddPhotoAsync(Guid driverId, Guid carId, IFormFile file, CancellationToken ct)
    {
        var car = await LoadOwnCarAsync(driverId, carId, ct);
        if (car.Photos.Count >= MaxPhotosPerCar)
            throw ApiException.BadRequest($"A car can have at most {MaxPhotosPerCar} photos.", "too_many_photos");

        var photo = new CarPhoto
        {
            CarId = car.Id,
            Url = await storage.SaveAsync($"cars/{car.Id}", file, ct),
            SortOrder = car.Photos.Count == 0 ? 0 : car.Photos.Max(p => p.SortOrder) + 1,
        };
        db.CarPhotos.Add(photo);
        await db.SaveChangesAsync(ct);
        return CarPhotoDto.From(photo);
    }

    public async Task DeletePhotoAsync(Guid driverId, Guid carId, Guid photoId, CancellationToken ct)
    {
        var car = await LoadOwnCarAsync(driverId, carId, ct);
        var photo = car.Photos.FirstOrDefault(p => p.Id == photoId) ?? throw ApiException.NotFound("Photo");
        db.CarPhotos.Remove(photo);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(photo.Url, ct);
    }

    /// <summary>Loads a (non-deleted) car with its photos and driver, or throws 404.</summary>
    public async Task<Car> LoadCarAsync(Guid carId, CancellationToken ct) =>
        await db.Cars
            .Include(c => c.Photos)
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == carId && c.DeletedAt == null, ct)
        ?? throw ApiException.NotFound("Car");

    private async Task<Car> LoadOwnCarAsync(Guid driverId, Guid carId, CancellationToken ct)
    {
        var car = await LoadCarAsync(carId, ct);
        if (car.DriverId != driverId)
            throw ApiException.NotFound("Car");
        return car;
    }

    private static void Apply(Car car, UpsertCarRequest request)
    {
        car.Make = request.Make.Trim();
        car.Model = request.Model.Trim();
        car.Year = request.Year;
        car.Color = request.Color.Trim();
        car.Category = request.Category;
        car.Transmission = request.Transmission;
        car.Horsepower = request.Horsepower;
        car.ZeroToHundredSeconds = request.ZeroToHundredSeconds;
        car.TopSpeedKmh = request.TopSpeedKmh;
        car.PassengerSeats = request.PassengerSeats;
        car.Description = request.Description?.Trim();
        car.LicensePlate = request.LicensePlate?.Trim().ToUpperInvariant();
        car.PricePerHour = request.PricePerHour;
        car.Currency = request.Currency ?? "EUR";
        car.IsActive = request.IsActive;
    }
}
