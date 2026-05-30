using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;

namespace SocialMediaWeb.Tests;

public class CommentServiceTests
{
    [Fact]
    public async Task CreateCommentAsync_WithExistingUserAndPost_PersistsComment()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, Email = "user@test.com", Role = "User" });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1, Descriprion = "Hello" });
        await context.SaveChangesAsync();

        var service = new CommentService(context);

        var comment = await service.CreateCommentAsync(new CreateCommentDto
        {
            Text = "Nice post",
            UserId = 1,
            PostId = 10
        });

        Assert.Equal("Nice post", comment.Text);
        Assert.Single(context.Comments);
    }

    [Fact]
    public async Task CreateCommentAsync_WithDeletedUser_ThrowsArgumentException()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, IsDeleted = true });
        context.Posts.Add(new SocialMediaPost { Id = 10, UserId = 1 });
        await context.SaveChangesAsync();

        var service = new CommentService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateCommentAsync(new CreateCommentDto
            {
                Text = "Blocked",
                UserId = 1,
                PostId = 10
            }));
    }
}
