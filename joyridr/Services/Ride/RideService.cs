using System.Globalization;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Interfaces;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Joyridr.Services;

/// <summary>
/// The ride lifecycle:
/// <code>
/// AwaitingPayment ──paid──▶ Requested ──driver confirms──▶ Confirmed ──start──▶ InProgress ──complete──▶ Completed
///        │                     │  └──driver declines──▶ Declined           │
///        │                     └──no answer in time──▶ Expired             │
///        └──payment not finished──▶ Expired       (rider/driver cancels)──▶ Cancelled
/// </code>
/// </summary>
public class RideService(
    AppDbContext db,
    AvailabilityService availability,
    IPaymentProvider payments,
    NotificationService notifications,
    IOptions<RideOptions> rideOptions,
    TimeProvider clock,
    ILogger<RideService> logger)
{
    private readonly RideOptions _options = rideOptions.Value;

    public async Task<RideDto> CreateAsync(Guid riderId, CreateRideRequest request, CancellationToken ct)
    {
        var car = await db.Cars
            .Include(c => c.Driver).ThenInclude(d => d.User)
            .FirstOrDefaultAsync(c => c.Id == request.CarId && c.DeletedAt == null, ct);
        if (car is null || !car.IsListed)
            throw ApiException.NotFound("Car");
        if (!car.Driver.IsAcceptingRides)
            throw ApiException.Conflict("This driver isn't accepting rides right now.", "driver_unavailable");
        if (car.DriverId == riderId)
            throw ApiException.BadRequest("You can't book your own car.", "own_car");

        var startsAt = request.StartsAt.ToUniversalTime();
        await availability.EnsureBookableAsync(car.Driver, startsAt, request.DurationMinutes, ct);

        var now = clock.GetUtcNow();
        var price = RidePricing.Calculate(car.PricePerHour, request.DurationMinutes);
        var ride = new Ride
        {
            RiderId = riderId,
            DriverId = car.DriverId,
            CarId = car.Id,
            Car = car,
            Driver = car.Driver.User,
            Status = RideStatus.AwaitingPayment,
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(request.DurationMinutes),
            DurationMinutes = request.DurationMinutes,
            MeetingPointType = request.MeetingPointType,
            RiderNote = request.Note?.Trim(),
            Price = price,
            Currency = car.Currency,
            Payment = new Payment
            {
                Provider = payments.Name,
                Amount = price,
                Currency = car.Currency,
                Status = PaymentStatus.Pending,
            },
        };
        SetInitialMeetingPoint(ride, car.Driver, request);

        // Save first so the database's overlap constraint reserves the slot *before* we touch
        // the rider's money. If two riders race for the same slot, one of them gets a 409 here.
        db.Rides.Add(ride);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsExclusionViolation())
        {
            throw ApiException.Conflict("Someone just booked that time slot. Please pick another one.", "slot_unavailable");
        }

        var result = await payments.AuthorizeAsync(ride.Id, price, car.Currency, ct);
        ride.Payment.ProviderPaymentId = result.ProviderPaymentId;

        if (result.Authorized)
        {
            MarkPaymentAuthorized(ride, car.Driver.User, now);
        }
        else if (result.FailureReason is not null)
        {
            ride.Payment.Status = PaymentStatus.Failed;
            ride.Payment.FailureReason = result.FailureReason;
            ride.Status = RideStatus.Cancelled;
            ride.CancelledAt = now;
            ride.CancellationReason = "Payment failed.";
            await db.SaveChangesAsync(ct);
            throw new ApiException(StatusCodes.Status402PaymentRequired, $"Payment failed: {result.FailureReason}", "payment_failed");
        }
        // Otherwise the payment needs action in the app (e.g. Stripe PaymentSheet / 3-D Secure).
        // The ride stays AwaitingPayment until the provider's webhook reports success.

        await db.SaveChangesAsync(ct);
        await notifications.SendPushesAsync(ct);
        return await GetAsync(riderId, ride.Id, ct, result.ClientSecret);
    }

    public async Task<RideDto> ConfirmAsync(Guid driverId, Guid rideId, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        EnsureDriver(ride, driverId);
        EnsureStatus(ride, RideStatus.Requested);
        var now = clock.GetUtcNow();
        if (ride.RespondBy < now)
            throw ApiException.Conflict("This request has expired.", "request_expired");

        await payments.CaptureAsync(ride.Payment!.ProviderPaymentId!, ct);
        ride.Payment.Status = PaymentStatus.Captured;
        ride.Payment.CapturedAt = now;

        ride.Status = RideStatus.Confirmed;
        ride.ConfirmedAt = now;
        if (ride.MeetingPointType == MeetingPointType.RiderComesToDriver)
        {
            // The driver's base is only revealed to the rider once the ride is confirmed.
            var driver = ride.Driver.DriverProfile!;
            ride.MeetingLatitude = driver.BaseLatitude;
            ride.MeetingLongitude = driver.BaseLongitude;
            ride.MeetingAddress = driver.BaseAddress;
        }

        notifications.Add(ride.RiderId, NotificationType.RideConfirmed, "Ride confirmed! 🏁",
            $"{UserNames.Display(ride.Driver)} confirmed your ride in the {CarName(ride.Car)} on {FormatStart(ride)}.", ride.Id);
        return await SaveAndReturnAsync(ride, driverId, ct);
    }

    public async Task<RideDto> DeclineAsync(Guid driverId, Guid rideId, string? reason, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        EnsureDriver(ride, driverId);
        EnsureStatus(ride, RideStatus.Requested);
        var now = clock.GetUtcNow();

        await ReleasePaymentAsync(ride, now, ct);
        ride.Status = RideStatus.Declined;
        ride.DeclinedAt = now;
        ride.DeclineReason = reason?.Trim();

        notifications.Add(ride.RiderId, NotificationType.RideDeclined, "Ride declined",
            $"Your ride in the {CarName(ride.Car)} on {FormatStart(ride)} was declined. You haven't been charged.", ride.Id);
        return await SaveAndReturnAsync(ride, driverId, ct);
    }

    public async Task<RideDto> CancelAsync(Guid userId, Guid rideId, string? reason, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        var now = clock.GetUtcNow();
        var payment = ride.Payment!;

        if (ride.RiderId == userId)
        {
            EnsureStatus(ride, RideStatus.AwaitingPayment, RideStatus.Requested, RideStatus.Confirmed);
            if (ride.Status == RideStatus.Confirmed)
            {
                var hoursBeforeStart = (ride.StartsAt - now).TotalHours;
                if (hoursBeforeStart >= _options.FreeCancellationHours)
                    await RefundAsync(payment, payment.Amount, now, ct);
                // Late cancellation: no refund. The driver blocked this time for the rider.
            }
            else
            {
                await ReleasePaymentAsync(ride, now, ct);
            }

            if (ride.Status != RideStatus.AwaitingPayment)
                notifications.Add(ride.DriverId, NotificationType.RideCancelled, "Ride cancelled",
                    $"{UserNames.Display(ride.Rider)} cancelled the ride on {FormatStart(ride)}.", ride.Id);
        }
        else if (ride.DriverId == userId)
        {
            if (ride.Status == RideStatus.Requested)
                throw ApiException.BadRequest("Use decline for ride requests.", "use_decline");
            EnsureStatus(ride, RideStatus.Confirmed);

            // Driver cancelling always means a full refund for the rider.
            await RefundAsync(payment, payment.Amount, now, ct);
            notifications.Add(ride.RiderId, NotificationType.RideCancelled, "Ride cancelled",
                $"{UserNames.Display(ride.Driver)} had to cancel your ride on {FormatStart(ride)}. You'll get a full refund.", ride.Id);
        }
        else
        {
            throw ApiException.NotFound("Ride");
        }

        ride.Status = RideStatus.Cancelled;
        ride.CancelledAt = now;
        ride.CancelledById = userId;
        ride.CancellationReason = reason?.Trim();
        return await SaveAndReturnAsync(ride, userId, ct);
    }

    public async Task<RideDto> UpdateMeetingPointAsync(Guid userId, Guid rideId, UpdateMeetingPointRequest request, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        if (ride.RiderId != userId && ride.DriverId != userId)
            throw ApiException.NotFound("Ride");
        EnsureStatus(ride, RideStatus.AwaitingPayment, RideStatus.Requested, RideStatus.Confirmed);

        switch (ride.MeetingPointType)
        {
            case MeetingPointType.RiderComesToDriver:
                throw ApiException.BadRequest("The meeting point is the driver's location for this ride.", "meeting_point_fixed");
            case MeetingPointType.DriverComesToRider:
                if (ride.RiderId != userId)
                    throw ApiException.Forbidden("Only the rider can change the pickup location.");
                EnsureWithinServiceArea(ride.Driver.DriverProfile!, request.Latitude, request.Longitude);
                break;
        }

        ride.MeetingLatitude = request.Latitude;
        ride.MeetingLongitude = request.Longitude;
        ride.MeetingAddress = request.Address?.Trim();

        var otherUserId = ride.RiderId == userId ? ride.DriverId : ride.RiderId;
        var who = ride.RiderId == userId ? ride.Rider : ride.Driver;
        notifications.Add(otherUserId, NotificationType.MeetingPointUpdated, "Meeting point updated",
            $"{UserNames.Display(who)} changed the meeting point to {request.Address ?? "a new location"}.", ride.Id);
        return await SaveAndReturnAsync(ride, userId, ct);
    }

    public async Task<RideDto> StartAsync(Guid driverId, Guid rideId, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        EnsureDriver(ride, driverId);
        EnsureStatus(ride, RideStatus.Confirmed);
        var now = clock.GetUtcNow();
        if (now < ride.StartsAt.AddMinutes(-30))
            throw ApiException.BadRequest("A ride can be started at most 30 minutes early.", "too_early");

        ride.Status = RideStatus.InProgress;
        ride.StartedAt = now;
        notifications.Add(ride.RiderId, NotificationType.RideStarted, "Your ride has started 🏎️", $"Enjoy the {CarName(ride.Car)}!", ride.Id);
        return await SaveAndReturnAsync(ride, driverId, ct);
    }

    public async Task<RideDto> CompleteAsync(Guid driverId, Guid rideId, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        EnsureDriver(ride, driverId);
        EnsureStatus(ride, RideStatus.InProgress);

        ride.Status = RideStatus.Completed;
        ride.CompletedAt = clock.GetUtcNow();
        ride.Driver.DriverProfile!.CompletedRides++;
        if (ride.Rider.RiderProfile is not null)
            ride.Rider.RiderProfile.CompletedRides++;

        notifications.Add(ride.RiderId, NotificationType.RideCompleted, "How was your ride?",
            $"Rate your ride with {UserNames.Display(ride.Driver)} in the {CarName(ride.Car)}.", ride.Id);
        return await SaveAndReturnAsync(ride, driverId, ct);
    }

    public async Task<ReviewDto> AddReviewAsync(Guid userId, Guid rideId, CreateReviewRequest request, CancellationToken ct)
    {
        var ride = await LoadForUpdateAsync(rideId, ct);
        if (ride.RiderId != userId && ride.DriverId != userId)
            throw ApiException.NotFound("Ride");
        EnsureStatus(ride, RideStatus.Completed);
        if (ride.Reviews.Any(r => r.AuthorId == userId))
            throw ApiException.Conflict("You already reviewed this ride.", "already_reviewed");

        var isRider = ride.RiderId == userId;
        var review = new Review
        {
            RideId = ride.Id,
            AuthorId = userId,
            Author = isRider ? ride.Rider : ride.Driver,
            SubjectId = isRider ? ride.DriverId : ride.RiderId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
        };
        db.Reviews.Add(review);

        if (isRider)
        {
            var profile = ride.Driver.DriverProfile!;
            (profile.RatingAverage, profile.RatingCount) = AddRating(profile.RatingAverage, profile.RatingCount, request.Rating);
        }
        else if (ride.Rider.RiderProfile is { } profile)
        {
            (profile.RatingAverage, profile.RatingCount) = AddRating(profile.RatingAverage, profile.RatingCount, request.Rating);
        }

        notifications.Add(review.SubjectId, NotificationType.NewReview, "You got a new review",
            $"{UserNames.Display(review.Author)} gave you {request.Rating}★.", ride.Id);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw ApiException.Conflict("You already reviewed this ride.", "already_reviewed");
        }
        await notifications.SendPushesAsync(ct);
        return ReviewDto.From(review);
    }

    public async Task<RideDto> GetAsync(Guid userId, Guid rideId, CancellationToken ct, string? clientSecret = null)
    {
        var ride = await RidesWithDetails().AsNoTracking().FirstOrDefaultAsync(r => r.Id == rideId, ct);
        if (ride is null || (ride.RiderId != userId && ride.DriverId != userId))
            throw ApiException.NotFound("Ride");
        return RideDto.From(ride, userId, clientSecret);
    }

    public async Task<PagedResult<RideDto>> ListAsync(Guid userId, UserRole role, RideListView view, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = RidesWithDetails().AsNoTracking();
        query = role == UserRole.JoyDriver ? query.Where(r => r.DriverId == userId) : query.Where(r => r.RiderId == userId);
        query = view switch
        {
            RideListView.Upcoming => query.Where(r => Ride.BlockingStatuses.Contains(r.Status)).OrderBy(r => r.StartsAt),
            RideListView.Past => query.Where(r => Ride.FinishedStatuses.Contains(r.Status)).OrderByDescending(r => r.StartsAt),
            RideListView.Requests => query.Where(r => r.Status == RideStatus.Requested).OrderBy(r => r.RespondBy),
            _ => throw ApiException.BadRequest("Unknown view.", "invalid_view"),
        };

        var total = await query.CountAsync(ct);
        var rides = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<RideDto>(rides.Select(r => RideDto.From(r, userId)).ToList(), page, pageSize, total);
    }

    /// <summary>
    /// Called every minute by <see cref="RideExpiryWorker"/>: releases slots whose payment was never
    /// finished, and expires requests the driver didn't answer in time.
    /// </summary>
    public async Task<int> ExpireStaleAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var paymentCutoff = now.AddMinutes(-_options.PaymentWindowMinutes);

        var stale = await db.Rides
            .Include(r => r.Payment)
            .Include(r => r.Car)
            .Include(r => r.Driver).ThenInclude(u => u.DriverProfile)
            .Where(r => (r.Status == RideStatus.AwaitingPayment && r.CreatedAt < paymentCutoff)
                        || (r.Status == RideStatus.Requested && r.RespondBy < now))
            .OrderBy(r => r.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        foreach (var ride in stale)
        {
            try
            {
                var wasRequested = ride.Status == RideStatus.Requested;
                await ReleasePaymentAsync(ride, now, ct);
                ride.Status = RideStatus.Expired;

                if (wasRequested)
                {
                    notifications.Add(ride.RiderId, NotificationType.RideExpired, "Ride request expired",
                        $"The driver didn't respond in time for the {CarName(ride.Car)} on {FormatStart(ride)}. You haven't been charged.", ride.Id);
                    notifications.Add(ride.DriverId, NotificationType.RideExpired, "Ride request expired",
                        $"You missed a ride request for the {CarName(ride.Car)} on {FormatStart(ride)}.", ride.Id);
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to expire ride {RideId}", ride.Id);
                db.ChangeTracker.Clear();
            }
        }

        await notifications.SendPushesAsync(ct);
        return stale.Count;
    }

    // ---- helpers ----

    private void SetInitialMeetingPoint(Ride ride, JoyDriverProfile driver, CreateRideRequest request)
    {
        switch (request.MeetingPointType)
        {
            case MeetingPointType.DriverComesToRider:
                if (request.MeetingLatitude is null || request.MeetingLongitude is null)
                    throw ApiException.BadRequest("Send your pickup location (meetingLatitude/meetingLongitude).", "meeting_point_required");
                EnsureWithinServiceArea(driver, request.MeetingLatitude.Value, request.MeetingLongitude.Value);
                ride.MeetingLatitude = request.MeetingLatitude;
                ride.MeetingLongitude = request.MeetingLongitude;
                ride.MeetingAddress = request.MeetingAddress?.Trim();
                break;

            case MeetingPointType.RiderComesToDriver:
                if (driver.BaseLatitude is null)
                    throw ApiException.Conflict("This driver hasn't set a pickup location yet. Choose another meeting option.", "driver_has_no_location");
                // Filled in from the driver's base when the ride is confirmed.
                break;

            case MeetingPointType.AgreedLocation:
                // Optional suggestion now; either side can change it later.
                if (request.MeetingLatitude is null != request.MeetingLongitude is null)
                    throw ApiException.BadRequest("Send both meetingLatitude and meetingLongitude, or neither.", "invalid_location");
                ride.MeetingLatitude = request.MeetingLatitude;
                ride.MeetingLongitude = request.MeetingLongitude;
                ride.MeetingAddress = request.MeetingAddress?.Trim();
                break;
        }
    }

    private static void EnsureWithinServiceArea(JoyDriverProfile driver, double latitude, double longitude)
    {
        if (driver.BaseLatitude is null || driver.BaseLongitude is null)
            throw ApiException.Conflict("This driver doesn't offer pickups yet. Choose another meeting option.", "driver_has_no_location");
        var distance = Geo.DistanceKm(driver.BaseLatitude.Value, driver.BaseLongitude.Value, latitude, longitude);
        if (distance > driver.ServiceRadiusKm)
            throw ApiException.BadRequest(
                $"That's {distance:0.#} km from the driver, who only picks up within {driver.ServiceRadiusKm} km.", "outside_service_area");
    }

    private void MarkPaymentAuthorized(Ride ride, User driver, DateTimeOffset now)
    {
        ride.Payment!.Status = PaymentStatus.Authorized;
        ride.Payment.AuthorizedAt = now;
        ride.Status = RideStatus.Requested;

        var respondBy = now.AddHours(_options.DriverResponseHours);
        var latest = ride.StartsAt.AddHours(-1);
        ride.RespondBy = respondBy < latest ? respondBy : latest;

        notifications.Add(driver.Id, NotificationType.RideRequested, "New ride request 🔑",
            $"Someone wants a ride on {FormatStart(ride)} for {ride.DurationMinutes} min. Respond by {FormatLocal(ride, ride.RespondBy.Value)}.", ride.Id);
    }

    /// <summary>Releases the hold on the rider's card for a ride that never got confirmed.</summary>
    private async Task ReleasePaymentAsync(Ride ride, DateTimeOffset now, CancellationToken ct)
    {
        var payment = ride.Payment;
        if (payment?.ProviderPaymentId is null || payment.Status is not (PaymentStatus.Pending or PaymentStatus.Authorized))
            return;
        await payments.VoidAsync(payment.ProviderPaymentId, ct);
        payment.Status = PaymentStatus.Voided;
        payment.UpdatedAt = now;
    }

    private async Task RefundAsync(Payment payment, decimal amount, DateTimeOffset now, CancellationToken ct)
    {
        if (payment.ProviderPaymentId is null || payment.Status != PaymentStatus.Captured || amount <= 0)
            return;
        await payments.RefundAsync(payment.ProviderPaymentId, amount, ct);
        payment.RefundedAmount += amount;
        payment.Status = payment.RefundedAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        payment.RefundedAt = now;
    }

    private static (double Average, int Count) AddRating(double average, int count, int rating) =>
        (Math.Round((average * count + rating) / (count + 1), 2), count + 1);

    private IQueryable<Ride> RidesWithDetails() =>
        db.Rides
            .Include(r => r.Car).ThenInclude(c => c.Photos)
            .Include(r => r.Rider).ThenInclude(u => u.RiderProfile)
            .Include(r => r.Driver).ThenInclude(u => u.DriverProfile)
            .Include(r => r.Payment)
            .Include(r => r.Reviews)
            .AsSplitQuery();

    private async Task<Ride> LoadForUpdateAsync(Guid rideId, CancellationToken ct) =>
        await RidesWithDetails().FirstOrDefaultAsync(r => r.Id == rideId, ct) ?? throw ApiException.NotFound("Ride");

    private async Task<RideDto> SaveAndReturnAsync(Ride ride, Guid viewerId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ApiException.Conflict("This ride was just changed by someone else. Refresh and try again.", "ride_changed");
        }
        await notifications.SendPushesAsync(ct);
        return RideDto.From(ride, viewerId);
    }

    private static void EnsureDriver(Ride ride, Guid driverId)
    {
        if (ride.DriverId != driverId)
            throw ApiException.NotFound("Ride");
    }

    private static void EnsureStatus(Ride ride, params RideStatus[] allowed)
    {
        if (!allowed.Contains(ride.Status))
            throw ApiException.Conflict($"Not possible while the ride is {ride.Status}.", "invalid_ride_status");
    }

    private static string CarName(Car car) => $"{car.Make} {car.Model}";

    private static string FormatStart(Ride ride) => FormatLocal(ride, ride.StartsAt);

    /// <summary>Formats a time in the driver's time zone, since that's where the ride happens.</summary>
    private static string FormatLocal(Ride ride, DateTimeOffset time)
    {
        var timeZoneId = ride.Driver?.DriverProfile?.TimeZoneId ?? ride.Car?.Driver?.TimeZoneId;
        var local = timeZoneId is not null && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var tz)
            ? TimeZoneInfo.ConvertTime(time, tz)
            : time;
        return local.ToString("ddd d MMM 'at' HH:mm", CultureInfo.InvariantCulture);
    }
}
