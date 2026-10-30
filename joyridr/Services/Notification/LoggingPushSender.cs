using Joyridr.Interfaces;

namespace Joyridr.Services;

/// <summary>Logs pushes instead of sending them. Replace with an APNs sender once the iOS app has push set up.</summary>
public class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task SendAsync(IReadOnlyCollection<string> deviceTokens, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        logger.LogInformation("Push to {DeviceCount} device(s): {Title} - {Body} {@Data}", deviceTokens.Count, title, body, data);
        return Task.CompletedTask;
    }
}
