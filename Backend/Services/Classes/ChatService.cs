using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes;

public sealed class ChatService : IChatService
{
    public const string QueueName = "chat-messages";
    private const int MaximumMessageLength = 2000;

    private readonly ApplicationDbContext _context;
    private readonly IServiceBusService _serviceBus;
    private readonly IChatMessageStore _messageStore;
    private readonly ILogger<ChatService>? _logger;

    public ChatService(
        ApplicationDbContext context,
        IServiceBusService serviceBus,
        IChatMessageStore messageStore,
        ILogger<ChatService>? logger = null)
    {
        _context = context;
        _serviceBus = serviceBus;
        _messageStore = messageStore;
        _logger = logger;
    }

    public async Task<QueuedChatMessageDto> QueueMessageAsync(
        int senderId,
        SendChatMessageDto request,
        CancellationToken cancellationToken = default)
    {
        if (senderId <= 0 || request.ReceiverId <= 0 || senderId == request.ReceiverId)
        {
            throw new ArgumentException("Sender and receiver must be different valid users.");
        }

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length > MaximumMessageLength)
        {
            throw new ArgumentException($"A chat message must contain between 1 and {MaximumMessageLength} characters.");
        }

        var activeUserCount = await _context.Users
            .CountAsync(user =>
                (user.Id == senderId || user.Id == request.ReceiverId) && !user.IsDeleted,
                cancellationToken);
        if (activeUserCount != 2)
        {
            throw new ArgumentException("Both chat participants must be active users.");
        }

        var messageId = request.ClientMessageId ?? Guid.NewGuid();
        var queueMessage = new ChatQueueMessage(messageId, senderId, request.ReceiverId, content, DateTime.UtcNow);

        // Immediately persist message to SQL so it is reliably available for both users
        await _messageStore.PersistAsync(queueMessage, cancellationToken);

        // Also publish to Service Bus for distributed subscribers/background functions if available
        try
        {
            await _serviceBus.SendMessageAsync(
                QueueName,
                queueMessage,
                messageId.ToString("N"));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to publish chat message {MessageId} to Service Bus. Message was saved to SQL directly.", messageId);
        }

        return new QueuedChatMessageDto { MessageId = messageId };
    }

    public async Task PersistIncomingMessageAsync(
        ChatQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        await _messageStore.PersistAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationAsync(
        int userId,
        int otherUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || otherUserId <= 0 || userId == otherUserId)
        {
            throw new ArgumentException("Conversation users must be different valid users.");
        }

        var mostRecentMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(message =>
                (message.SenderId == userId && message.ReceiverId == otherUserId) ||
                (message.SenderId == otherUserId && message.ReceiverId == userId))
            .OrderByDescending(message => message.SentAt)
            .ThenByDescending(message => message.Id)
            .Take(limit)
            .Select(message => new ChatMessageDto
            {
                Id = message.Id,
                MessageId = message.MessageId,
                SenderId = message.SenderId,
                ReceiverId = message.ReceiverId,
                Content = message.Content,
                SentAt = message.SentAt,
                DeliveredAt = message.DeliveredAt,
                IsRead = message.IsRead
            })
            .ToListAsync(cancellationToken);

        return mostRecentMessages
            .OrderBy(message => message.SentAt)
            .ThenBy(message => message.Id)
            .ToList();
    }

    public async Task<int> MarkConversationReadAsync(
        int userId,
        int otherUserId,
        CancellationToken cancellationToken = default)
    {
        var unreadMessages = await _context.ChatMessages
            .Where(message =>
                message.SenderId == otherUserId &&
                message.ReceiverId == userId &&
                !message.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        if (unreadMessages.Count > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return unreadMessages.Count;
    }
}
