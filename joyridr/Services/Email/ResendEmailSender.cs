using System.Net.Http.Headers;
using Joyridr.Common;
using Joyridr.Interfaces;
using Microsoft.Extensions.Options;

namespace Joyridr.Services;

/// <summary>
/// Sends email through Resend's HTTP API (https://resend.com/docs/api-reference/emails/send-email).
/// HTTP rather than SMTP, because Railway doesn't allow outgoing SMTP on every plan.
/// </summary>
public class ResendEmailSender(HttpClient http, IOptions<EmailOptions> options, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(new
            {
                from = options.Value.From,
                to = new[] { message.To },
                subject = message.Subject,
                html = message.Html,
                text = message.Text,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ResendApiKey);

        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Resend explains the problem in the body, e.g. an unverified domain. It never contains the key.
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Resend answered {(int)response.StatusCode}: {body}");
        }
        logger.LogInformation("Sent the email \"{Subject}\" through Resend.", message.Subject.Split(':')[0]);
    }
}
