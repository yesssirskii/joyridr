namespace Joyridr.Services;

/// <summary>Runs <see cref="RideService.ExpireStaleAsync"/> once a minute.</summary>
public class RideExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<RideExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expired = await scope.ServiceProvider.GetRequiredService<RideService>().ExpireStaleAsync(stoppingToken);
                if (expired > 0)
                    logger.LogInformation("Expired {Count} stale ride(s)", expired);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Ride expiry run failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
