using Microsoft.AspNetCore.Http;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class PostFeedTests
{
    [Fact]
    public async Task GetFeedAsync_ReturnsNewestFirstWithCountsAndViewerLike()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, Name = "Author", Role = "User" });
        context.Users.Add(new User { Id = 2, Name = "Viewer", Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1, CreatedAt = new DateTime(2026, 1, 1) });
        context.Posts.Add(new SocialMediaPost { Id = 11, UserId = 1, CreatedAt = new DateTime(2026, 2, 1) });
        context.Likes.Add(new Like { PostId = 10, UserId = 2 });
        context.Comments.Add(new Comment { PostId = 10, UserId = 2, Text = "Hi" });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var feed = await service.GetFeedAsync(viewerId: 2, skip: 0, take: 20);

        Assert.Equal(new[] { 11, 10 }, feed.Select(p => p.Id));
        var older = feed[1];
        Assert.Equal(1, older.LikeCount);
        Assert.Equal(1, older.CommentCount);
        Assert.True(older.IsLiked);
        Assert.Equal("Author", older.User?.Name);
        Assert.False(feed[0].IsLiked);
    }

    [Fact]
    public async Task DeletePostAsync_ByAnotherUser_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1 });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeletePostAsync(10, userId: 2));
        Assert.True(await service.DeletePostAsync(10, userId: 1));
        Assert.Empty(context.Posts);
    }

    private static SocialMedia CreateService(ApplicationDbContext context)
    {
        return new SocialMedia(context, new PassThroughBlobStorage(), new NotificationPublisher(new NoOpServiceBus()));
    }

    private sealed class PassThroughBlobStorage : IBlobStorageService
    {
        public Task<string> UploadAsync(IFormFile file, string containerName) => Task.FromResult(file.FileName);
        public Task DeleteAsync(string blobUrl, string containerName) => Task.CompletedTask;
        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
    }

    private sealed class NoOpServiceBus : IServiceBusService
    {
        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null) => Task.CompletedTask;
        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null) => Task.FromResult(default(T));
    }
}
