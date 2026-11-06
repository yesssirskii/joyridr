using Joyridr.Interfaces;

namespace Joyridr.Services;

/// <summary>Writes emails to the log instead of sending them. Used when no Resend key is set (Local without a key, tests).</summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation("Email to {To} (not sent, no Resend key): {Subject}\n{Text}", message.To, message.Subject, message.Text);
        return Task.CompletedTask;
    }
}
