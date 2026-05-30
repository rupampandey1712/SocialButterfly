using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;

namespace SocialMediaWeb.Tests;

public class AdminServiceTests
{
    [Fact]
    public async Task GetUserDetail_ExcludesAdminUsers()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Name = "Regular", Email = "user@test.com", Role = "User" },
            new User { Id = 2, Name = "Admin", Email = "admin@test.com", Role = "Admin" });
        await context.SaveChangesAsync();
        var service = new AdminService(context);

        var users = (await service.GetUserDetail()).ToList();

        Assert.Single(users);
        Assert.Equal("Regular", users[0].Name);
    }

    [Fact]
    public async Task UpdateIsUserDelete_WithExistingUser_UpdatesFlag()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1, IsDeleted = false });
        await context.SaveChangesAsync();
        var service = new AdminService(context);

        await service.UpdateIsUserDelete(1, new UpdateUserFlag { IsDeleted = true });

        Assert.True(context.Users.Single().IsDeleted);
    }
}
