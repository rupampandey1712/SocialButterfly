using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes;

public sealed class NotificationStore : INotificationStore
{
    private readonly ApplicationDbContext _context;
    private readonly IBlobStorageService? _blobStorage;

    public NotificationStore(ApplicationDbContext context, IBlobStorageService? blobStorage = null)
    {
        _context = context;
        _blobStorage = blobStorage;
    }

    public async Task ProcessAsync(NotificationEvent notification, CancellationToken cancellationToken = default)
    {
        if (notification.EventId == Guid.Empty || notification.ActorId <= 0)
        {
            throw new ArgumentException("The queued notification is invalid.", nameof(notification));
        }

        if (notification.Type == NotificationTypes.PostUnliked)
        {
            await RemoveLikeNotificationsAsync(notification.ActorId, notification.PostId, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return;
        }

        var recipients = await ResolveRecipientsAsync(notification, cancellationToken);
        recipients.Remove(notification.ActorId);
        await RemoveMutedRecipientsAsync(recipients, notification.Type, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        // Service Bus is at-least-once; skip recipients this event already reached.
        var alreadyDelivered = await _context.Notifications
            .Where(n => n.EventId == notification.EventId)
            .Select(n => n.RecipientId)
            .ToListAsync(cancellationToken);
        recipients.ExceptWith(alreadyDelivered);
        if (recipients.Count == 0)
        {
            return;
        }

        if (notification.Type == NotificationTypes.PostLiked)
        {
            // Re-liking a post bumps the existing activity instead of stacking duplicates.
            await RemoveLikeNotificationsAsync(notification.ActorId, notification.PostId, cancellationToken);
        }

        foreach (var recipientId in recipients)
        {
            _context.Notifications.Add(new Notification
            {
                EventId = notification.EventId,
                Type = notification.Type,
                ActorId = notification.ActorId,
                RecipientId = recipientId,
                PostId = notification.PostId,
                Text = notification.Text,
                CreatedAt = notification.OccurredAt.Kind == DateTimeKind.Utc
                    ? notification.OccurredAt
                    : notification.OccurredAt.ToUniversalTime()
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _context.ChangeTracker.Clear();
            if (!await _context.Notifications.AnyAsync(n => n.EventId == notification.EventId, cancellationToken))
            {
                throw;
            }

            // A competing delivery of the same event persisted first.
        }
    }

    public async Task<List<NotificationDto>> GetForUserAsync(int userId, int take)
    {
        var notifications = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientId == userId && n.Actor != null && !n.Actor.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(take, 1, 100))
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Text = n.Text,
                CreatedAt = n.CreatedAt,
                IsRead = n.IsRead,
                PostId = n.PostId,
                PostImagePath = n.Post == null ? null : n.Post.ImagePath,
                Actor = new PostAuthorDto
                {
                    Id = n.ActorId,
                    Name = n.Actor!.Name,
                    ImagePath = n.Actor.ImagePath
                }
            })
            .ToListAsync();

        if (_blobStorage != null)
        {
            foreach (var notification in notifications)
            {
                notification.PostImagePath = _blobStorage.ToPublicUrl(notification.PostImagePath);
                notification.Actor!.ImagePath = _blobStorage.ToPublicUrl(notification.Actor.ImagePath);
            }
        }

        return notifications;
    }

    public Task<int> GetUnreadCountAsync(int userId)
    {
        return _context.Notifications.CountAsync(n => n.RecipientId == userId && !n.IsRead);
    }

    public async Task MarkAllReadAsync(int userId)
    {
        var unread = await _context.Notifications
            .Where(n => n.RecipientId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    private async Task<HashSet<int>> ResolveRecipientsAsync(NotificationEvent notification, CancellationToken cancellationToken)
    {
        switch (notification.Type)
        {
            case NotificationTypes.PostLiked:
            {
                // Messages are not ordered: if the like was already undone, there is nothing to announce.
                var stillLiked = await _context.Likes.AnyAsync(
                    l => l.PostId == notification.PostId && l.UserId == notification.ActorId, cancellationToken);
                return stillLiked ? await PostAuthorAsync(notification.PostId, cancellationToken) : new HashSet<int>();
            }

            case NotificationTypes.PostCommented:
                return await PostAuthorAsync(notification.PostId, cancellationToken);

            case NotificationTypes.PostCreated:
            {
                // Friends and followers hear about new posts.
                var actorId = notification.ActorId;
                var audience = await _context.Friends
                    .Where(f => f.IsAccepted && (f.SenderId == actorId || f.ReceiverId == actorId))
                    .Select(f => f.SenderId == actorId ? f.ReceiverId : f.SenderId)
                    .ToListAsync(cancellationToken);
                audience.AddRange(await _context.Follows
                    .Where(f => f.FollowingId == actorId)
                    .Select(f => (int?)f.FollowerId)
                    .ToListAsync(cancellationToken));
                return await ActiveUsersAsync(audience, cancellationToken);
            }

            case NotificationTypes.FriendRequestSent:
            case NotificationTypes.FriendRequestAccepted:
            case NotificationTypes.UserFollowed:
                return await ActiveUsersAsync(new List<int?> { notification.RecipientId }, cancellationToken);

            default:
                throw new ArgumentException($"Unknown notification type '{notification.Type}'.", nameof(notification));
        }
    }

    /// <summary>Drops recipients who turned this kind of notification off in Settings.</summary>
    private async Task RemoveMutedRecipientsAsync(HashSet<int> recipients, string type, CancellationToken cancellationToken)
    {
        if (recipients.Count == 0)
        {
            return;
        }

        var ids = recipients.ToList();
        var users = _context.Users.Where(u => ids.Contains(u.Id));
        users = type switch
        {
            NotificationTypes.PostLiked => users.Where(u => u.MuteLikeNotifications),
            NotificationTypes.PostCommented => users.Where(u => u.MuteCommentNotifications),
            NotificationTypes.PostCreated => users.Where(u => u.MuteNewPostNotifications),
            NotificationTypes.UserFollowed or NotificationTypes.FriendRequestSent or NotificationTypes.FriendRequestAccepted
                => users.Where(u => u.MuteFollowNotifications),
            _ => users.Where(_ => false)
        };

        recipients.ExceptWith(await users.Select(u => u.Id).ToListAsync(cancellationToken));
    }

    private async Task<HashSet<int>> PostAuthorAsync(int? postId, CancellationToken cancellationToken)
    {
        var authorId = await _context.Posts
            .Where(p => p.Id == postId)
            .Select(p => p.UserId)
            .FirstOrDefaultAsync(cancellationToken);
        return await ActiveUsersAsync(new List<int?> { authorId }, cancellationToken);
    }

    private async Task<HashSet<int>> ActiveUsersAsync(List<int?> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id > 0).Select(id => id!.Value).Distinct().ToList();
        var active = await _context.Users
            .Where(u => ids.Contains(u.Id) && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        return active.ToHashSet();
    }

    private async Task RemoveLikeNotificationsAsync(int actorId, int? postId, CancellationToken cancellationToken)
    {
        var existing = await _context.Notifications
            .Where(n => n.Type == NotificationTypes.PostLiked && n.ActorId == actorId && n.PostId == postId)
            .ToListAsync(cancellationToken);
        _context.Notifications.RemoveRange(existing);
    }
}
