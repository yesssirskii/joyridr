using Joyridr.Common;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;

namespace Joyridr.Controllers;

/// <summary>
/// Stripe calls this when a payment changes. Every request is checked against the webhook's signing
/// secret, so nobody else can mark a ride as paid. Set the endpoint up in the Stripe dashboard
/// (Developers › Webhooks) with the payment_intent.* events below.
/// </summary>
[Route("api/payments/stripe/webhook")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class StripeWebhookController(RideService rides, IOptions<StripeOptions> options, ILogger<StripeWebhookController> logger)
    : ControllerBase
{
    private static readonly HashSet<string> HandledEvents =
    [
        "payment_intent.amount_capturable_updated", // the money is held: send the request to the driver
        "payment_intent.payment_failed",            // card declined: remember why, the rider can retry
        "payment_intent.canceled",
    ];

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        var secret = options.Value.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
            return NotFound();

        // Stripe always signs its calls; without the header Stripe's library would crash instead of rejecting it.
        string? signature = Request.Headers["Stripe-Signature"];
        if (string.IsNullOrWhiteSpace(signature))
            return BadRequest();

        var json = await new StreamReader(Request.Body).ReadToEndAsync(ct);
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, secret, throwOnApiVersionMismatch: false);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Rejected a Stripe webhook: {Message}", ex.Message);
            return BadRequest();
        }

        if (HandledEvents.Contains(stripeEvent.Type) && stripeEvent.Data.Object is PaymentIntent intent)
            await rides.SyncPaymentByProviderIdAsync(intent.Id, ct);

        // 200 for everything else too, so Stripe doesn't keep retrying events we don't use.
        return Ok();
    }
}
