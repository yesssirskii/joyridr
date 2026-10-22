namespace Joyridr.Services;

/// <summary>
/// Result of starting a payment. With a real provider (Stripe) the payment usually isn't authorized
/// straight away: the app gets <see cref="ClientSecret"/>, completes payment with the Stripe SDK, and
/// a webhook tells us when it's authorized.
/// </summary>
public record PaymentAuthorizationResult(string ProviderPaymentId, bool Authorized, string? ClientSecret, string? FailureReason);

/// <summary>
/// Hold money at booking, take it when the driver confirms, release or refund it otherwise.
/// Maps directly onto Stripe PaymentIntents with capture_method=manual.
/// </summary>
public interface IPaymentProvider
{
    string Name { get; }
    Task<PaymentAuthorizationResult> AuthorizeAsync(Guid rideId, decimal amount, string currency, CancellationToken ct);
    Task CaptureAsync(string providerPaymentId, CancellationToken ct);
    Task VoidAsync(string providerPaymentId, CancellationToken ct);
    Task RefundAsync(string providerPaymentId, decimal amount, CancellationToken ct);
}
