namespace Joyridr.Common;

/// <summary>Stripe settings, bound from the "Stripe" config section.</summary>
public class StripeOptions
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Secret API key (sk_test_… or sk_live_…). Never commit it: set it with user-secrets or the
    /// Stripe__SecretKey variable. When it's empty, the fake payment provider is used instead.
    /// </summary>
    public string? SecretKey { get; set; }

    /// <summary>Publishable key (pk_test_…). Not secret; the app gets it from the payment-sheet endpoint.</summary>
    public string? PublishableKey { get; set; }

    /// <summary>Signing secret of the webhook endpoint (whsec_…). Without it, webhooks are rejected.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>Apple Pay merchant ID registered in the Apple Developer account and in Stripe.</summary>
    public string ApplePayMerchantId { get; set; } = "merchant.org.joyridr";

    /// <summary>Where the business is registered, for Apple Pay.</summary>
    public string MerchantCountryCode { get; set; } = "HR";

    /// <summary>Shown on the payment sheet and on the rider's bank statement description.</summary>
    public string MerchantDisplayName { get; set; } = "JOYRIDR";
}
