using Joyridr.Models.Enums;

namespace Joyridr.Models;

/// <summary>
/// One payment per ride. Money is authorized (held) when the rider books, captured when the
/// driver confirms, and voided/refunded if the ride doesn't happen.
/// </summary>
public class Payment
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid RideId { get; set; }
    public Ride Ride { get; set; } = null!;

    /// <summary>"fake" for now, later e.g. "stripe".</summary>
    public string Provider { get; set; } = null!;
    public string? ProviderPaymentId { get; set; }

    public decimal Amount { get; set; }
    public decimal RefundedAmount { get; set; }
    public string Currency { get; set; } = "EUR";
    public PaymentStatus Status { get; set; }
    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }
}
