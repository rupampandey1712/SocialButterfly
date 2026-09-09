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

    [Fact]
    public async Task GetMyFriends_ReturnsFriendsBidirectionally()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = true, AcceptedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new FriendService(context);

        var senderFriends = (await service.GetMyFriends(1)).Value;
        var receiverFriends = (await service.GetMyFriends(2)).Value;

        Assert.NotNull(senderFriends);
        Assert.Single(senderFriends);
        Assert.Equal(2, senderFriends[0].Id);

        Assert.NotNull(receiverFriends);
        Assert.Single(receiverFriends);
        Assert.Equal(1, receiverFriends[0].Id);
    }

    [Fact]
    public async Task DeleteOrDeclineFriendRequest_RemovesRecord()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = false });
        await context.SaveChangesAsync();
        var service = new FriendService(context);

        var deleted = await service.DeleteOrDeclineFriendRequest(1, 2);

        Assert.True(deleted);
        Assert.Empty(context.Friends);
    }

    [Fact]
    public async Task GetFriendshipStatus_ReturnsCorrectStatus()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        var service = new FriendService(context);

        Assert.Equal("None", await service.GetFriendshipStatus(1, 2));

        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = false });
        await context.SaveChangesAsync();

        Assert.Equal("RequestSent", await service.GetFriendshipStatus(1, 2));
        Assert.Equal("RequestReceived", await service.GetFriendshipStatus(2, 1));

        context.Friends.Single().IsAccepted = true;
        await context.SaveChangesAsync();

        Assert.Equal("Friends", await service.GetFriendshipStatus(1, 2));
        Assert.Equal("Friends", await service.GetFriendshipStatus(2, 1));
    }

    private static void SeedUsers(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { Id = 1, Email = "sender@test.com", Role = "User" },
            new User { Id = 2, Email = "receiver@test.com", Role = "User" });
        context.SaveChanges();
    }
}
