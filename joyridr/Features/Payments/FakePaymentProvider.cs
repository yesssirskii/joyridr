namespace Joyridr.Features.Payments;

/// <summary>Development stand-in: every payment succeeds instantly. Replace with a Stripe implementation before launch.</summary>
public class FakePaymentProvider(ILogger<FakePaymentProvider> logger) : IPaymentProvider
{
    public string Name => "fake";

    public Task<PaymentAuthorizationResult> AuthorizeAsync(Guid rideId, decimal amount, string currency, CancellationToken ct)
    {
        var id = $"fake_{Guid.NewGuid():N}";
        logger.LogInformation("Fake payment {PaymentId}: authorized {Amount} {Currency} for ride {RideId}", id, amount, currency, rideId);
        return Task.FromResult(new PaymentAuthorizationResult(id, Authorized: true, ClientSecret: null, FailureReason: null));
    }

    public Task CaptureAsync(string providerPaymentId, CancellationToken ct)
    {
        logger.LogInformation("Fake payment {PaymentId}: captured", providerPaymentId);
        return Task.CompletedTask;
    }

    public Task VoidAsync(string providerPaymentId, CancellationToken ct)
    {
        logger.LogInformation("Fake payment {PaymentId}: voided", providerPaymentId);
        return Task.CompletedTask;
    }

    public Task RefundAsync(string providerPaymentId, decimal amount, CancellationToken ct)
    {
        logger.LogInformation("Fake payment {PaymentId}: refunded {Amount}", providerPaymentId, amount);
        return Task.CompletedTask;
    }
}
