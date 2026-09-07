using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes;

public sealed class ChatMessageStore : IChatMessageStore
{
    private const int MaximumMessageLength = 2000;
    private readonly ApplicationDbContext _context;

    public ChatMessageStore(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task PersistAsync(ChatQueueMessage message, CancellationToken cancellationToken = default)
    {
        if (message.MessageId == Guid.Empty ||
            message.SenderId <= 0 ||
            message.ReceiverId <= 0 ||
            message.SenderId == message.ReceiverId ||
            string.IsNullOrWhiteSpace(message.Content) ||
            message.Content.Length > MaximumMessageLength)
        {
            throw new ArgumentException("The queued chat message is invalid.", nameof(message));
        }

        // Service Bus and Durable Task are both at-least-once systems. The unique
        // index is the final idempotency guard for all retries and redeliveries.
        if (await _context.ChatMessages.AnyAsync(chat => chat.MessageId == message.MessageId, cancellationToken))
        {
            return;
        }

        var activeUserCount = await _context.Users
            .CountAsync(user =>
                (user.Id == message.SenderId || user.Id == message.ReceiverId) && !user.IsDeleted,
                cancellationToken);
        if (activeUserCount != 2)
        {
            throw new InvalidOperationException("A chat participant no longer exists or is inactive.");
        }

        _context.ChatMessages.Add(new ChatMessage
        {
            MessageId = message.MessageId,
            SenderId = message.SenderId,
            ReceiverId = message.ReceiverId,
            Content = message.Content.Trim(),
            SentAt = message.SentAt.Kind == DateTimeKind.Utc
                ? message.SentAt
                : message.SentAt.ToUniversalTime(),
            DeliveredAt = DateTime.UtcNow
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            if (!await _context.ChatMessages
                .AnyAsync(chat => chat.MessageId == message.MessageId, cancellationToken))
            {
                throw;
            }

            // A competing durable activity persisted the same message first.
        }
    }
}
