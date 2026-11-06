using System.Threading.Channels;
using Joyridr.Interfaces;

namespace Joyridr.Services;

/// <summary>
/// Emails are sent in the background, so a request doesn't wait for Resend. That also keeps
/// "forgot password" equally fast whether or not the account exists, so it can't be used to find out.
/// If the API restarts before an email goes out, it's lost; the user can ask for a new code.
/// </summary>
public class EmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Enqueue(EmailMessage message) => _channel.Writer.TryWrite(message);

    public IAsyncEnumerable<EmailMessage> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public class EmailDispatcher(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The address is personal data, so only the subject is logged.
                logger.LogError(ex, "Couldn't send the email \"{Subject}\"", message.Subject);
            }
        }
    }
}
