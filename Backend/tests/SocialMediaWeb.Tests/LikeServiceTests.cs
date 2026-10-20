using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;

namespace SocialMediaWeb.Tests;

public class LikeServiceTests
{
    [Fact]
    public async Task LikeCountAsync_WithValidUserAndPost_AddsOneLike()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUserAndPost(context);
        var service = new LikeService(context);

        var count = await service.LikeCountAsync(postId: 10, id: 1);

        Assert.Equal(1, count);
        Assert.Single(context.Likes);
    }

    [Fact]
    public async Task LikeCountAsync_WhenUserAlreadyLiked_ReturnsExistingCountWithoutDuplicate()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUserAndPost(context);
        context.Likes.Add(new Like { UserId = 1, PostId = 10 });
        await context.SaveChangesAsync();
        var service = new LikeService(context);

        var count = await service.LikeCountAsync(postId: 10, id: 1);

        Assert.Equal(1, count);
        Assert.Single(context.Likes);
    }

    [Fact]
    public async Task LikeCountAsync_WithMissingPost_ReturnsNegativeOne()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, Role = "User" });
        await context.SaveChangesAsync();
        var service = new LikeService(context);

        var count = await service.LikeCountAsync(postId: 99, id: 1);

        Assert.Equal(-1, count);
    }

    [Fact]
    public async Task ToggleLikeAsync_LikesThenUnlikes()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUserAndPost(context);
        var service = new LikeService(context);

        var liked = await service.ToggleLikeAsync(postId: 10, userId: 1);
        Assert.NotNull(liked);
        Assert.True(liked!.IsLiked);
        Assert.Equal(1, liked.LikeCount);

        var unliked = await service.ToggleLikeAsync(postId: 10, userId: 1);
        Assert.NotNull(unliked);
        Assert.False(unliked!.IsLiked);
        Assert.Equal(0, unliked.LikeCount);
        Assert.Empty(context.Likes);
    }

    [Fact]
    public async Task ToggleLikeAsync_WithMissingPost_ReturnsNull()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUserAndPost(context);
        var service = new LikeService(context);

        Assert.Null(await service.ToggleLikeAsync(postId: 99, userId: 1));
    }

    private static void SeedUserAndPost(ApplicationDbContext context)
    {
        context.Users.Add(new User { Id = 1, Email = "user@test.com", Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1 });
        context.SaveChanges();
    }
}
