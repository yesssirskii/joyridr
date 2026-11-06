using Joyridr.Models.Enums;

namespace Joyridr.Models;

/// <summary>A user reporting another user, one of their chat messages, or a review they wrote. Admins work through these.</summary>
public class Report
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid ReporterId { get; set; }
    public User Reporter { get; set; } = null!;
    /// <summary>Who the report is about: the user, or the author of the message or review.</summary>
    public Guid ReportedUserId { get; set; }
    public User ReportedUser { get; set; } = null!;

    public ReportKind Kind { get; set; }
    /// <summary>Set for message reports. Becomes null if the message is removed.</summary>
    public Guid? MessageId { get; set; }
    public ChatMessage? Message { get; set; }
    /// <summary>Set for review reports. Becomes null if the review is removed.</summary>
    public Guid? ReviewId { get; set; }
    public Review? Review { get; set; }

    /// <summary>The message or review text when it was reported, so admins can still read it after it's removed.</summary>
    public string? ContentSnapshot { get; set; }

    public ReportReason Reason { get; set; }
    public string? Details { get; set; }

    public ReportStatus Status { get; set; } = ReportStatus.Open;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedById { get; set; }
    /// <summary>What was done, e.g. "Message removed" or "User banned".</summary>
    public string? Resolution { get; set; }
}
