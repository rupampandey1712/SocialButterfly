using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;

namespace SocialMediaWeb.Tests;

public class FriendServiceTests
{
    [Fact]
    public async Task SendFriendRequest_WithValidUsers_CreatesPendingRequest()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        var service = new FriendService(context);

        var result = await service.SendFriendRequest(new FriendDto
        {
            SenderId = 1,
            ReceiverId = 2
        });

        Assert.False(result.Value!.IsAccepted);
        Assert.Equal(1, result.Value.SenderId);
        Assert.Equal(2, result.Value.ReceiverId);
        Assert.Single(context.Friends);
    }

    [Fact]
    public async Task SendFriendRequest_ForSameUser_ThrowsArgumentException()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        var service = new FriendService(context);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendFriendRequest(new FriendDto
            {
                SenderId = 1,
                ReceiverId = 1
            }));
    }

    [Fact]
    public async Task SendFriendRequest_WhenRequestAlreadyExists_ThrowsInvalidOperationException()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        context.Friends.Add(new Friend { SenderId = 2, ReceiverId = 1 });
        await context.SaveChangesAsync();
        var service = new FriendService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SendFriendRequest(new FriendDto
            {
                SenderId = 1,
                ReceiverId = 2
            }));
    }

    [Fact]
    public async Task AcceptFriendRequest_WithExistingSender_MarksRequestAccepted()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = false });
        await context.SaveChangesAsync();
        var service = new FriendService(context);

        var result = await service.AcceptFriendRequest(1);

        Assert.True(result.Value!.IsAccepted);
        Assert.True(context.Friends.Single().IsAccepted);
    }

    private static void SeedUsers(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { Id = 1, Email = "sender@test.com", Role = "User" },
            new User { Id = 2, Email = "receiver@test.com", Role = "User" });
        context.SaveChanges();
    }
}
