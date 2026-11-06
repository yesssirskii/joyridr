using System.ComponentModel.DataAnnotations;

namespace Joyridr.DTOs;

/// <summary>The person on the other side of a conversation.</summary>
public record ChatParticipantDto(Guid Id, string DisplayName, string? AvatarUrl, bool IsDriver);

/// <summary>
/// One row in the chat inbox. <c>CanMessage</c> is false when either side blocked the other, or the other
/// account was deleted or banned. <c>BlockedByMe</c> is true when the viewer did the blocking, so the app can offer to unblock.
/// </summary>
public record ConversationDto(
    Guid Id,
    ChatParticipantDto OtherUser,
    ChatMessageDto? LastMessage,
    int UnreadCount,
    DateTimeOffset CreatedAt,
    bool CanMessage,
    bool BlockedByMe);

/// <summary><see cref="IsMine"/> tells the app which side to draw the bubble on.</summary>
public record ChatMessageDto(Guid Id, Guid ConversationId, Guid SenderId, bool IsMine, string Body, DateTimeOffset CreatedAt);

/// <summary>Rider: any driver's user id. Driver: a rider who has booked one of your cars.</summary>
public record StartConversationRequest(Guid OtherUserId);

public record SendMessageRequest([Required, MaxLength(2000)] string Body);
