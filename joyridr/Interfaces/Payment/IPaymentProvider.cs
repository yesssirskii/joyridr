namespace Joyridr.Interfaces;

/// <summary>
/// Result of starting a payment. With a real provider (Stripe) the payment usually isn't authorized
/// straight away: the app gets <see cref="ClientSecret"/>, completes payment with the Stripe SDK, and
/// a webhook (or the app asking us to check) tells us when it's authorized.
/// </summary>
public record PaymentAuthorizationResult(string ProviderPaymentId, bool Authorized, string? ClientSecret, string? FailureReason);

/// <summary>Where a payment stands at the provider.</summary>
public enum ProviderPaymentState
{
    /// <summary>The rider hasn't finished paying yet (or has to try again with another card).</summary>
    Pending,
    /// <summary>The money is held and can be captured.</summary>
    Authorized,
    Captured,
    Canceled,
}

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

    /// <summary>Asks the provider where the payment stands, plus the last error the rider got, if any.</summary>
    Task<(ProviderPaymentState State, string? LastError)> GetStateAsync(string providerPaymentId, CancellationToken ct);

    /// <summary>What the app needs to show the payment sheet again, e.g. after it was closed. Null when not needed.</summary>
    Task<string?> GetClientSecretAsync(string providerPaymentId, CancellationToken ct);
}
