using Joyridr.DTOs;
using Joyridr.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Joyridr.Controllers;

/// <summary>Rider–driver chat.</summary>
[Route("api/conversations")]
[Authorize]
public class ConversationsController(ChatService chat) : ApiControllerBase
{
    /// <summary>The chat inbox, most recent first, with the last message and unread count per conversation.</summary>
    [HttpGet]
    public Task<List<ConversationDto>> List(CancellationToken ct) => chat.ListAsync(CurrentUserId, ct);

    /// <summary>Total unread messages, for the badge on the chat icon.</summary>
    [HttpGet("unread-count")]
    public Task<UnreadCountDto> UnreadCount(CancellationToken ct) => chat.GetUnreadCountAsync(CurrentUserId, ct);

    /// <summary>Open the conversation with a driver (riders) or with a rider who booked you (drivers). Creates it on first contact.</summary>
    [HttpPost]
    public Task<ConversationDto> Start(StartConversationRequest request, CancellationToken ct) =>
        chat.StartAsync(CurrentUserId, CurrentUserRole, request.OtherUserId, ct);

    /// <summary>One conversation.</summary>
    [HttpGet("{conversationId:guid}")]
    public Task<ConversationDto> Get(Guid conversationId, CancellationToken ct) => chat.GetAsync(CurrentUserId, conversationId, ct);

    /// <summary>
    /// Messages, oldest first. Poll with afterMessageId (the newest id you have) every few seconds while
    /// the chat is open; pass beforeMessageId (the oldest id you have) to load earlier messages.
    /// </summary>
    [HttpGet("{conversationId:guid}/messages")]
    public Task<List<ChatMessageDto>> Messages(Guid conversationId, CancellationToken ct,
        Guid? afterMessageId = null, Guid? beforeMessageId = null, int limit = 50) =>
        chat.GetMessagesAsync(CurrentUserId, conversationId, afterMessageId, beforeMessageId, limit, ct);

    /// <summary>Send a message. The other person gets a push notification.</summary>
    [HttpPost("{conversationId:guid}/messages")]
    public async Task<ActionResult<ChatMessageDto>> Send(Guid conversationId, SendMessageRequest request, CancellationToken ct)
    {
        var message = await chat.SendAsync(CurrentUserId, conversationId, request.Body, ct);
        return CreatedAtAction(nameof(Messages), new { conversationId }, message);
    }

    /// <summary>Mark the conversation as read (call when the chat screen is shown).</summary>
    [HttpPost("{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid conversationId, CancellationToken ct)
    {
        await chat.MarkReadAsync(CurrentUserId, conversationId, ct);
        return NoContent();
    }
}
