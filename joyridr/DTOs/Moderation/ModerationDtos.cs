using System.ComponentModel.DataAnnotations;
using Joyridr.Common;
using Joyridr.Models;
using Joyridr.Models.Enums;

namespace Joyridr.DTOs;

public record BlockUserRequest(Guid UserId);

/// <summary>Someone you blocked, for the list in your profile where you can unblock them.</summary>
public record BlockedUserDto(Guid Id, string DisplayName, string? AvatarUrl, DateTimeOffset BlockedAt);

/// <summary>Set exactly one of <see cref="UserId"/>, <see cref="MessageId"/> or <see cref="ReviewId"/>.</summary>
public record CreateReportRequest(
    Guid? UserId,
    Guid? MessageId,
    Guid? ReviewId,
    ReportReason Reason,
    [MaxLength(1000)] string? Details);

/// <summary>Confirms the account deletion with the user's current password.</summary>
public record DeleteAccountRequest([Required, MaxLength(256)] string Password);

public record BanUserRequest([MaxLength(500)] string? Reason);

/// <summary>A user as admins see them in the report queue.</summary>
public record ReportUserDto(
    Guid Id,
    string DisplayName,
    string Username,
    string? AvatarUrl,
    UserRole Role,
    bool IsBanned,
    bool IsDeleted)
{
    public static ReportUserDto From(User u) => new(
        u.Id, UserNames.Display(u), u.Username, u.AvatarUrl, u.Role, u.BannedAt is not null, u.DeletedAt is not null);
}

public record ReportDto(
    Guid Id,
    ReportKind Kind,
    ReportReason Reason,
    string? Details,
    /// <summary>The message or review text as it was when reported.</summary>
    string? ContentSnapshot,
    /// <summary>False once the message or review has been removed. Always false for user reports.</summary>
    bool ContentExists,
    ReportUserDto Reporter,
    ReportUserDto ReportedUser,
    /// <summary>All open reports about the same user, including this one.</summary>
    int OpenReportsAboutUser,
    ReportStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? Resolution);
