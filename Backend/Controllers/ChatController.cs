using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;
using System.Security.Claims;

namespace SocialMediaWeb.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public sealed class ChatController : ControllerBase
{
    private const int DefaultConversationLimit = 50;
    private const int MaximumConversationLimit = 100;

    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost]
    public async Task<ActionResult<QueuedChatMessageDto>> Send(
        SendChatMessageDto request,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var result = await _chatService.QueueMessageAsync(currentUserId.Value, request, cancellationToken);
            return Accepted(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("with/{otherUserId:int}")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetConversation(
        int otherUserId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        try
        {
            var safeLimit = Math.Clamp(limit ?? DefaultConversationLimit, 1, MaximumConversationLimit);
            var messages = await _chatService.GetConversationAsync(
                currentUserId.Value,
                otherUserId,
                safeLimit,
                cancellationToken);
            return Ok(messages);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPut("with/{otherUserId:int}/read")]
    public async Task<IActionResult> MarkRead(int otherUserId, CancellationToken cancellationToken)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
        {
            return Unauthorized();
        }

        var updated = await _chatService.MarkConversationReadAsync(
            currentUserId.Value,
            otherUserId,
            cancellationToken);
        return Ok(new { markedRead = updated });
    }

    private int? GetCurrentUserId() => User.GetUserId();
}
