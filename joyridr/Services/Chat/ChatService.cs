using Joyridr.Common;
using Joyridr.Data;
using Joyridr.DTOs;
using Joyridr.Models;
using Joyridr.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Joyridr.Services;

/// <summary>
/// Rider–driver chat over plain HTTP: the app polls for new messages while a chat is open and
/// gets a push for messages that arrive while it isn't. (SignalR/WebSockets can come later.)
/// </summary>
public class ChatService(AppDbContext db, NotificationService notifications, TimeProvider clock)
{
    private const int MaxPushPreviewLength = 120;

    public async Task<List<ConversationDto>> ListAsync(Guid userId, CancellationToken ct)
    {
        var mine = db.Conversations
            .Where(c => c.RiderId == userId || c.DriverId == userId)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .Take(100);
        var rows = await ToRows(mine, userId).ToListAsync(ct);
        return rows.Select(r => r.ToDto(userId)).ToList();
    }

    /// <summary>Opens the conversation with another user, creating it on first contact.</summary>
    public async Task<ConversationDto> StartAsync(Guid userId, UserRole role, Guid otherUserId, CancellationToken ct)
    {
        Guid riderId, driverId;
        switch (role)
        {
            case UserRole.JoyRider:
                if (!await db.DriverProfiles.AnyAsync(d => d.UserId == otherUserId, ct))
                    throw ApiException.NotFound("Driver");
                (riderId, driverId) = (userId, otherUserId);
                break;
            case UserRole.JoyDriver:
                // Drivers can't cold-message riders; only riders who have booked them.
                if (!await db.Rides.AnyAsync(r => r.DriverId == userId && r.RiderId == otherUserId, ct))
                    throw ApiException.Forbidden("You can only message riders who have booked one of your cars.");
                (riderId, driverId) = (otherUserId, userId);
                break;
            default:
                throw ApiException.Forbidden("Only riders and drivers can chat.");
        }

        var conversationId = await db.Conversations
            .Where(c => c.RiderId == riderId && c.DriverId == driverId)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(ct);

        if (conversationId is null)
        {
            var conversation = new Conversation { RiderId = riderId, DriverId = driverId };
            db.Conversations.Add(conversation);
            try
            {
                await db.SaveChangesAsync(ct);
                conversationId = conversation.Id;
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                // Both sides opened the chat at the same moment; use the one that won.
                db.ChangeTracker.Clear();
                conversationId = await db.Conversations
                    .Where(c => c.RiderId == riderId && c.DriverId == driverId)
                    .Select(c => c.Id)
                    .FirstAsync(ct);
            }
        }

        return await GetAsync(userId, conversationId.Value, ct);
    }

    public async Task<ConversationDto> GetAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        var conversation = db.Conversations.Where(c => c.Id == conversationId && (c.RiderId == userId || c.DriverId == userId));
        var row = await ToRows(conversation, userId).FirstOrDefaultAsync(ct)
                  ?? throw ApiException.NotFound("Conversation");
        return row.ToDto(userId);
    }

    /// <summary>
    /// Messages oldest-first. With <paramref name="afterMessageId"/>: only newer ones (for polling).
    /// With <paramref name="beforeMessageId"/>: the page before it (for scrolling up). Neither: the latest page.
    /// </summary>
    public async Task<List<ChatMessageDto>> GetMessagesAsync(
        Guid userId, Guid conversationId, Guid? afterMessageId, Guid? beforeMessageId, int limit, CancellationToken ct)
    {
        await EnsureParticipantAsync(userId, conversationId, ct);
        limit = Math.Clamp(limit, 1, 100);
        var query = db.ChatMessages.Where(m => m.ConversationId == conversationId);

        List<ChatMessage> messages;
        if (afterMessageId is { } afterId)
        {
            var after = await MessageTimeAsync(conversationId, afterId, ct);
            messages = await query.Where(m => m.CreatedAt > after).OrderBy(m => m.CreatedAt).Take(limit).ToListAsync(ct);
        }
        else
        {
            if (beforeMessageId is { } beforeId)
            {
                var before = await MessageTimeAsync(conversationId, beforeId, ct);
                query = query.Where(m => m.CreatedAt < before);
            }
            messages = await query.OrderByDescending(m => m.CreatedAt).Take(limit).ToListAsync(ct);
            messages.Reverse();
        }

        return messages.Select(m => ToDto(m, userId)).ToList();
    }

    public async Task<ChatMessageDto> SendAsync(Guid userId, Guid conversationId, string body, CancellationToken ct)
    {
        var conversation = await db.Conversations
            .Include(c => c.Rider)
            .Include(c => c.Driver)
            .FirstOrDefaultAsync(c => c.Id == conversationId && (c.RiderId == userId || c.DriverId == userId), ct)
            ?? throw ApiException.NotFound("Conversation");

        var text = body.Trim();
        if (text.Length == 0)
            throw ApiException.BadRequest("Message can't be empty.", "empty_message");

        var now = clock.GetUtcNow();
        var message = new ChatMessage { ConversationId = conversation.Id, SenderId = userId, Body = text, CreatedAt = now };
        db.ChatMessages.Add(message);
        conversation.LastMessageAt = now;

        var senderIsRider = conversation.RiderId == userId;
        // Sending a message means you've seen everything before it.
        if (senderIsRider)
            conversation.RiderLastReadAt = now;
        else
            conversation.DriverLastReadAt = now;
        await db.SaveChangesAsync(ct);

        var sender = senderIsRider ? conversation.Rider : conversation.Driver;
        var recipientId = senderIsRider ? conversation.DriverId : conversation.RiderId;
        await notifications.PushAsync(recipientId, UserNames.Display(sender),
            text.Length <= MaxPushPreviewLength ? text : text[..MaxPushPreviewLength] + "…",
            new Dictionary<string, string>
            {
                ["type"] = "chatMessage",
                ["conversationId"] = conversation.Id.ToString(),
                ["messageId"] = message.Id.ToString(),
            }, ct);

        return ToDto(message, userId);
    }

    /// <summary>Marks everything in the conversation as read for the current user.</summary>
    public async Task MarkReadAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var updated = await db.Conversations.Where(c => c.Id == conversationId && c.RiderId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.RiderLastReadAt, now), ct);
        updated += await db.Conversations.Where(c => c.Id == conversationId && c.DriverId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.DriverLastReadAt, now), ct);
        if (updated == 0)
            throw ApiException.NotFound("Conversation");
    }

    /// <summary>Total unread messages across all conversations, for the badge on the chat icon.</summary>
    public async Task<UnreadCountDto> GetUnreadCountAsync(Guid userId, CancellationToken ct) =>
        new(await db.ChatMessages.CountAsync(m => m.SenderId != userId &&
            ((m.Conversation.RiderId == userId && (m.Conversation.RiderLastReadAt == null || m.CreatedAt > m.Conversation.RiderLastReadAt)) ||
             (m.Conversation.DriverId == userId && (m.Conversation.DriverLastReadAt == null || m.CreatedAt > m.Conversation.DriverLastReadAt))), ct));

    // ---- helpers ----

    /// <summary>
    /// Adds the other user, last message and unread count. Filter and sort <paramref name="conversations"/>
    /// before calling this: EF can't translate sorting on the projected record.
    /// </summary>
    private static IQueryable<ConversationRow> ToRows(IQueryable<Conversation> conversations, Guid userId) =>
        conversations
            .Select(c => new ConversationRow(
                c,
                c.Rider,
                c.Driver,
                c.Messages.OrderByDescending(m => m.CreatedAt).FirstOrDefault(),
                c.Messages.Count(m => m.SenderId != userId
                    && ((c.RiderId == userId ? c.RiderLastReadAt : c.DriverLastReadAt) == null
                        || m.CreatedAt > (c.RiderId == userId ? c.RiderLastReadAt : c.DriverLastReadAt)))));

    private async Task EnsureParticipantAsync(Guid userId, Guid conversationId, CancellationToken ct)
    {
        if (!await db.Conversations.AnyAsync(c => c.Id == conversationId && (c.RiderId == userId || c.DriverId == userId), ct))
            throw ApiException.NotFound("Conversation");
    }

    private async Task<DateTimeOffset> MessageTimeAsync(Guid conversationId, Guid messageId, CancellationToken ct) =>
        await db.ChatMessages
            .Where(m => m.Id == messageId && m.ConversationId == conversationId)
            .Select(m => (DateTimeOffset?)m.CreatedAt)
            .FirstOrDefaultAsync(ct)
        ?? throw ApiException.NotFound("Message");

    private static ChatMessageDto ToDto(ChatMessage m, Guid viewerId) =>
        new(m.Id, m.ConversationId, m.SenderId, m.SenderId == viewerId, m.Body, m.CreatedAt);

    private record ConversationRow(Conversation Conversation, User Rider, User Driver, ChatMessage? LastMessage, int UnreadCount)
    {
        public ConversationDto ToDto(Guid viewerId)
        {
            var viewerIsRider = Conversation.RiderId == viewerId;
            var other = viewerIsRider ? Driver : Rider;
            return new ConversationDto(
                Conversation.Id,
                new ChatParticipantDto(other.Id, UserNames.Display(other), other.AvatarUrl, IsDriver: viewerIsRider),
                LastMessage is null ? null : ChatService.ToDto(LastMessage, viewerId),
                UnreadCount,
                Conversation.CreatedAt);
        }
    }
}
