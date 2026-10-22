namespace Joyridr.Services;

public interface IPushSender
{
    Task SendAsync(IReadOnlyCollection<string> deviceTokens, string title, string body, IReadOnlyDictionary<string, string> data, CancellationToken ct);
}
