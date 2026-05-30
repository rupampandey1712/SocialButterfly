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

    private static void SeedUserAndPost(ApplicationDbContext context)
    {
        context.Users.Add(new User { Id = 1, Email = "user@test.com", Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1 });
        context.SaveChanges();
    }
}
