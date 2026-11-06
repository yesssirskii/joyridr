using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>Users report people, chat messages and reviews; admins work through them in <see cref="AdminService"/>.</summary>
public class ReportService(AppDbContext db)
{
    public async Task CreateAsync(Guid reporterId, CreateReportRequest request, CancellationToken ct)
    {
        var targets = (request.UserId is null ? 0 : 1) + (request.MessageId is null ? 0 : 1) + (request.ReviewId is null ? 0 : 1);
        if (targets != 1)
            throw ApiException.BadRequest("Report one user, message or review at a time.", "invalid_report");

        var report = new Report
        {
            ReporterId = reporterId,
            Reason = request.Reason,
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
        };

        if (request.MessageId is { } messageId)
        {
            // Only people in the conversation can see (and so report) a message.
            var message = await db.ChatMessages
                .Where(m => m.Id == messageId && (m.Conversation.RiderId == reporterId || m.Conversation.DriverId == reporterId))
                .FirstOrDefaultAsync(ct) ?? throw ApiException.NotFound("Message");
            report.Kind = ReportKind.Message;
            report.MessageId = message.Id;
            report.ReportedUserId = message.SenderId;
            report.ContentSnapshot = message.Body;
        }
        else if (request.ReviewId is { } reviewId)
        {
            var review = await db.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct) ?? throw ApiException.NotFound("Review");
            report.Kind = ReportKind.Review;
            report.ReviewId = review.Id;
            report.ReportedUserId = review.AuthorId;
            report.ContentSnapshot = string.IsNullOrEmpty(review.Comment) ? $"{review.Rating}★" : $"{review.Rating}★ {review.Comment}";
        }
        else
        {
            var userId = request.UserId!.Value;
            if (!await db.Users.AnyAsync(u => u.Id == userId && u.DeletedAt == null, ct))
                throw ApiException.NotFound("User");
            report.Kind = ReportKind.User;
            report.ReportedUserId = userId;
        }

        if (report.ReportedUserId == reporterId)
            throw ApiException.BadRequest("You can't report yourself.", "report_self");

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
    }
}
