using System.Security.Claims;
using Joyridr.Common;
using Joyridr.Data;
using Joyridr.Domain;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Features.Notifications;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        notifications.MapGet("/", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct, bool unreadOnly = false, int page = 1, int pageSize = 30) =>
        {
            var userId = principal.GetUserId();
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = db.Notifications.Where(n => n.UserId == userId);
            if (unreadOnly)
                query = query.Where(n => n.ReadAt == null);

            var total = await query.CountAsync(ct);
            var items = await query.OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .ToListAsync(ct);
            return new PagedResult<NotificationDto>(items.Select(NotificationDto.From).ToList(), page, pageSize, total);
        });

        notifications.MapGet("/unread-count", async (ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            return new { count = await db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct) };
        }).WithSummary("For the badge on the notifications tab.");

        notifications.MapPost("/{notificationId:guid}/read", async (Guid notificationId, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var now = clock.GetUtcNow();
            await db.Notifications.Where(n => n.Id == notificationId && n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
            return Results.NoContent();
        });

        notifications.MapPost("/read-all", async (ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var now = clock.GetUtcNow();
            await db.Notifications.Where(n => n.UserId == userId && n.ReadAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
            return Results.NoContent();
        });

        var devices = app.MapGroup("/api/devices").WithTags("Notifications").RequireAuthorization();

        devices.MapPost("/", async (RegisterDeviceRequest request, ClaimsPrincipal principal, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            var now = clock.GetUtcNow();
            // A device token belongs to whoever logged in on that phone most recently.
            var device = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == request.Token, ct);
            if (device is null)
                db.DeviceTokens.Add(new DeviceToken { UserId = userId, Token = request.Token, LastSeenAt = now });
            else
            {
                device.UserId = userId;
                device.LastSeenAt = now;
            }
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).WithSummary("Register the APNs device token after the user allows notifications. Call on every app launch.");

        devices.MapDelete("/{token}", async (string token, ClaimsPrincipal principal, AppDbContext db, CancellationToken ct) =>
        {
            var userId = principal.GetUserId();
            await db.DeviceTokens.Where(d => d.Token == token && d.UserId == userId).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        }).WithSummary("Unregister on logout.");

        return app;
    }
}
