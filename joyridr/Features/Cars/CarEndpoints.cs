using System.Security.Claims;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Joyridr.Features.Drivers;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Cars;

public static class CarEndpoints
{
    private const int MaxCarsPerDriver = 10;
    private const int MaxPhotosPerCar = 12;

    public static IEndpointRouteBuilder MapCarEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cars").WithTags("Cars").RequireAuthorization();

        group.MapGet("/nearby", SearchNearby)
            .WithSummary("Listed cars whose driver is based within radiusKm of the given point, closest first.");

        group.MapGet("/{carId:guid}", async (Guid carId, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var car = await LoadCar(db, carId, ct);
            var isOwner = car.DriverId == principal.GetUserId();
            if (!car.IsListed && !isOwner && principal.GetRole() != UserRole.Admin)
                throw ApiException.NotFound("Car");
            return CarDetailDto.From(car, isOwner);
        });

        group.MapPost("/", async (UpsertCarRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var driverId = principal.GetUserId();
            if (await db.Cars.CountAsync(c => c.DriverId == driverId && c.DeletedAt == null, ct) >= MaxCarsPerDriver)
                throw ApiException.BadRequest($"You can have at most {MaxCarsPerDriver} cars.", "too_many_cars");

            var car = new Car { DriverId = driverId };
            Apply(car, request);
            db.Cars.Add(car);
            await db.SaveChangesAsync(ct);

            var created = await LoadCar(db, car.Id, ct);
            return Results.Created($"/api/cars/{car.Id}", CarDetailDto.From(created, isOwner: true));
        }).RequireAuthorization(Policies.Driver).WithSummary("Add a car. It stays hidden until an admin approves it.");

        group.MapPut("/{carId:guid}", async (Guid carId, UpsertCarRequest request, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var car = await LoadOwnCar(db, carId, principal, ct);

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
        }).RequireAuthorization(Policies.Driver);

        group.MapDelete("/{carId:guid}", async (Guid carId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var car = await LoadOwnCar(db, carId, principal, ct);
            var hasUpcomingRides = await db.Rides.AnyAsync(r => r.CarId == carId && Ride.BlockingStatuses.Contains(r.Status), ct);
            if (hasUpcomingRides)
                throw ApiException.Conflict("This car has upcoming rides. Cancel or finish them first.", "car_has_rides");

            // Soft delete: past rides still point at this car.
            car.DeletedAt = clock.GetUtcNow();
            car.IsActive = false;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.Driver);

        group.MapPost("/{carId:guid}/photos", async (Guid carId, IFormFile file, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var car = await LoadOwnCar(db, carId, principal, ct);
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
            return Results.Created(photo.Url, CarPhotoDto.From(photo));
        }).RequireAuthorization(Policies.Driver).DisableAntiforgery()
          .WithSummary("Upload a car photo (multipart/form-data, field name \"file\"). The first photo is the cover.");

        group.MapDelete("/{carId:guid}/photos/{photoId:guid}", async (Guid carId, Guid photoId, ClaimsPrincipal principal, AppDbContext db, IFileStorage storage, CancellationToken ct) =>
        {
            var car = await LoadOwnCar(db, carId, principal, ct);
            var photo = car.Photos.FirstOrDefault(p => p.Id == photoId) ?? throw ApiException.NotFound("Photo");
            db.CarPhotos.Remove(photo);
            await db.SaveChangesAsync(ct);
            await storage.DeleteAsync(photo.Url, ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.Driver);

        return app;
    }

    private static async Task<PagedResult<NearbyCarDto>> SearchNearby(
        double latitude,
        double longitude,
        AppDbContext db,
        CancellationToken ct,
        double radiusKm = 25,
        string? category = null,
        int? minSeats = null,
        decimal? maxPricePerHour = null,
        int page = 1,
        int pageSize = 20)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw ApiException.BadRequest("Invalid coordinates.", "invalid_location");
        radiusKm = Math.Clamp(radiusKm, 1, 200);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var (minLat, maxLat, minLng, maxLng) = Geo.BoundingBox(latitude, longitude, radiusKm);

        var query = db.Cars
            .Where(c => c.Status == CarStatus.Approved && c.IsActive && c.DeletedAt == null)
            .Where(c => c.Driver.IsAcceptingRides
                        && c.Driver.BaseLatitude >= minLat && c.Driver.BaseLatitude <= maxLat
                        && c.Driver.BaseLongitude >= minLng && c.Driver.BaseLongitude <= maxLng);
        if (QueryEnum.Parse<CarCategory>(category, nameof(category)) is { } carCategory)
            query = query.Where(c => c.Category == carCategory);
        if (minSeats is not null)
            query = query.Where(c => c.PassengerSeats >= minSeats);
        if (maxPricePerHour is not null)
            query = query.Where(c => c.PricePerHour <= maxPricePerHour);

        // The bounding box narrows things down in SQL; exact distance and sorting happen here.
        // Good for an MVP; switch to PostGIS once there are thousands of cars.
        var candidates = await query
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .Include(c => c.Photos)
            .AsSplitQuery()
            .ToListAsync(ct);

        var results = candidates
            .Select(c => (Car: c, Distance: Geo.DistanceKm(latitude, longitude, c.Driver.BaseLatitude!.Value, c.Driver.BaseLongitude!.Value)))
            .Where(x => x.Distance <= radiusKm)
            .OrderBy(x => x.Distance)
            .ToList();

        var items = results
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NearbyCarDto(
                CarSummaryDto.From(x.Car),
                DriverSummaryDto.From(x.Car.Driver),
                Math.Round(x.Distance, 1),
                Math.Round(x.Car.Driver.BaseLatitude!.Value, 2),
                Math.Round(x.Car.Driver.BaseLongitude!.Value, 2)))
            .ToList();

        return new PagedResult<NearbyCarDto>(items, page, pageSize, results.Count);
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

    internal static async Task<Car> LoadCar(AppDbContext db, Guid carId, CancellationToken ct) =>
        await db.Cars
            .Include(c => c.Photos)
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == carId && c.DeletedAt == null, ct)
        ?? throw ApiException.NotFound("Car");

    private static async Task<Car> LoadOwnCar(AppDbContext db, Guid carId, ClaimsPrincipal principal, CancellationToken ct)
    {
        var car = await LoadCar(db, carId, ct);
        if (car.DriverId != principal.GetUserId())
            throw ApiException.NotFound("Car");
        return car;
    }
}
