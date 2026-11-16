using Joyridr.Common;
using Joyridr.Interfaces;
using Microsoft.Extensions.Options;
using Stripe;

namespace Joyridr.Services;

/// <summary>
/// Stripe PaymentIntents with capture_method=manual:
/// booking creates the intent (the app pays it with the payment sheet, which holds the money),
/// the driver confirming captures it, and a ride that doesn't happen cancels or refunds it.
/// Card holds last about 7 days, which is plenty: drivers answer within 24 hours.
/// </summary>
public class StripePaymentProvider(IStripeClient stripe, IOptions<StripeOptions> options, ILogger<StripePaymentProvider> logger)
    : IPaymentProvider
{
    public string Name => "stripe";

    private readonly PaymentIntentService _intents = new(stripe);
    private readonly RefundService _refunds = new(stripe);

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(Guid rideId, decimal amount, string currency, CancellationToken ct)
    {
        var intent = await _intents.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnits(amount),
            Currency = currency.ToLowerInvariant(),
            CaptureMethod = "manual",
            // Cards and Apple Pay; no payment methods that send the rider off to another website.
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true, AllowRedirects = "never" },
            Description = $"{options.Value.MerchantDisplayName} ride",
            Metadata = new Dictionary<string, string> { ["ride_id"] = rideId.ToString() },
        }, new RequestOptions { IdempotencyKey = $"ride-{rideId}-authorize" }, ct);

        logger.LogInformation("Stripe payment {PaymentId} created for ride {RideId}", intent.Id, rideId);
        return new PaymentAuthorizationResult(intent.Id, Authorized: false, intent.ClientSecret, FailureReason: null);
    }

    public async Task CaptureAsync(string providerPaymentId, CancellationToken ct)
    {
        if (IsFake(providerPaymentId)) return;
        await _intents.CaptureAsync(providerPaymentId, cancellationToken: ct);
        logger.LogInformation("Stripe payment {PaymentId} captured", providerPaymentId);
    }

    public async Task VoidAsync(string providerPaymentId, CancellationToken ct)
    {
        if (IsFake(providerPaymentId)) return;
        try
        {
            await _intents.CancelAsync(providerPaymentId, cancellationToken: ct);
            logger.LogInformation("Stripe payment {PaymentId} canceled", providerPaymentId);
        }
        catch (StripeException ex) when (ex.StripeError?.PaymentIntent?.Status == "canceled")
        {
            // Already released, e.g. a retry after a timeout. Nothing left to do.
        }
    }

    public async Task RefundAsync(string providerPaymentId, decimal amount, CancellationToken ct)
    {
        if (IsFake(providerPaymentId)) return;
        await _refunds.CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = providerPaymentId,
            Amount = ToMinorUnits(amount),
        }, new RequestOptions { IdempotencyKey = $"{providerPaymentId}-refund-{ToMinorUnits(amount)}" }, ct);
        logger.LogInformation("Stripe payment {PaymentId} refunded {Amount}", providerPaymentId, amount);
    }

    public async Task<(ProviderPaymentState State, string? LastError)> GetStateAsync(string providerPaymentId, CancellationToken ct)
    {
        if (IsFake(providerPaymentId)) return (ProviderPaymentState.Authorized, null);
        var intent = await _intents.GetAsync(providerPaymentId, cancellationToken: ct);
        var state = intent.Status switch
        {
            "requires_capture" => ProviderPaymentState.Authorized,
            "succeeded" => ProviderPaymentState.Captured,
            "canceled" => ProviderPaymentState.Canceled,
            _ => ProviderPaymentState.Pending, // requires_payment_method, requires_action, processing…
        };
        return (state, intent.LastPaymentError?.Message);
    }

    public async Task<string?> GetClientSecretAsync(string providerPaymentId, CancellationToken ct) =>
        IsFake(providerPaymentId) ? null : (await _intents.GetAsync(providerPaymentId, cancellationToken: ct)).ClientSecret;

    /// <summary>
    /// Bookings made before Stripe was switched on were paid with <see cref="FakePaymentProvider"/>.
    /// That money never existed at Stripe, so capturing, releasing or refunding it simply succeeds.
    /// </summary>
    private static bool IsFake(string providerPaymentId) => providerPaymentId.StartsWith("fake_", StringComparison.Ordinal);

    /// <summary>12.50 EUR → 1250 cents. JOYRIDR only uses two-decimal currencies (EUR).</summary>
    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
