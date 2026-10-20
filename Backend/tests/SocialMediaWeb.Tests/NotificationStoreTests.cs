using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class NotificationStoreTests
{
    [Fact]
    public async Task ProcessAsync_PostLiked_NotifiesAuthorOnceEvenWhenRedelivered()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        context.Likes.Add(new Like { PostId = 10, UserId = 2 });
        await context.SaveChangesAsync();
        var store = new NotificationStore(context);
        var liked = Event(NotificationTypes.PostLiked, actorId: 2, postId: 10);

        await store.ProcessAsync(liked);
        await store.ProcessAsync(liked);

        var notification = Assert.Single(context.Notifications);
        Assert.Equal(1, notification.RecipientId);
        Assert.Equal(1, await store.GetUnreadCountAsync(1));
    }

    [Fact]
    public async Task ProcessAsync_PostLikedAfterUnlike_IsSkipped()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        var store = new NotificationStore(context);

        await store.ProcessAsync(Event(NotificationTypes.PostLiked, actorId: 2, postId: 10));

        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task ProcessAsync_PostUnliked_RemovesLikeNotification()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        context.Likes.Add(new Like { PostId = 10, UserId = 2 });
        await context.SaveChangesAsync();
        var store = new NotificationStore(context);
        await store.ProcessAsync(Event(NotificationTypes.PostLiked, actorId: 2, postId: 10));

        await store.ProcessAsync(Event(NotificationTypes.PostUnliked, actorId: 2, postId: 10));

        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task ProcessAsync_OwnPostComment_DoesNotNotifySelf()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        var store = new NotificationStore(context);

        await store.ProcessAsync(Event(NotificationTypes.PostCommented, actorId: 1, postId: 10, text: "mine"));

        Assert.Empty(context.Notifications);
    }

    [Fact]
    public async Task ProcessAsync_PostCreated_FansOutToAcceptedFriendsOnly()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        context.Users.Add(new User { Id = 3, Name = "Pending", Role = "User" });
        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = true });
        context.Friends.Add(new Friend { SenderId = 3, ReceiverId = 1, IsAccepted = false });
        await context.SaveChangesAsync();
        var store = new NotificationStore(context);

        await store.ProcessAsync(Event(NotificationTypes.PostCreated, actorId: 1, postId: 10));

        var notification = Assert.Single(context.Notifications);
        Assert.Equal(2, notification.RecipientId);
    }

    [Fact]
    public async Task MarkAllReadAsync_ClearsUnreadCount()
    {
        await using var context = TestDbContextFactory.Create();
        Seed(context);
        var store = new NotificationStore(context);
        await store.ProcessAsync(Event(NotificationTypes.FriendRequestSent, actorId: 2, recipientId: 1));

        await store.MarkAllReadAsync(1);

        Assert.Equal(0, await store.GetUnreadCountAsync(1));
        var feed = await store.GetForUserAsync(1, take: 10);
        Assert.Equal("Viewer", Assert.Single(feed).Actor?.Name);
    }

    [Fact]
    public async Task NotificationPublisher_WhenServiceBusFails_DoesNotThrow()
    {
        var publisher = new NotificationPublisher(new FailingServiceBus());

        await publisher.PublishAsync(NotificationTypes.PostLiked, actorId: 2, postId: 10);
    }

    private static NotificationEvent Event(string type, int actorId, int? recipientId = null, int? postId = null, string? text = null)
        => new(Guid.NewGuid(), type, actorId, recipientId, postId, text, DateTime.UtcNow);

    private static void Seed(ApplicationDbContext context)
    {
        context.Users.Add(new User { Id = 1, Name = "Author", Role = "User" });
        context.Users.Add(new User { Id = 2, Name = "Viewer", Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1 });
        context.SaveChanges();
    }

    private sealed class FailingServiceBus : IServiceBusService
    {
        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null)
            => throw new InvalidOperationException("Service Bus is down");

        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null)
            => Task.FromResult(default(T));
    }
}
