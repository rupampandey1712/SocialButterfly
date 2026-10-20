using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Controllers;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

/// <summary>Serialized API responses must never contain credentials or private contact data.</summary>
public class ResponseLeakTests
{
    [Fact]
    public async Task FriendsList_ContainsOnlyPublicFields()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Name = "Ava", Email = "ava@test.com", PhoneNumber = "+14155550100", PasswordHash = "$2a$secret-hash" },
            new User { Id = 2, Name = "Leo", Email = "leo@test.com", PasswordHash = "$2a$another-hash" });
        context.Friends.Add(new Friend { SenderId = 1, ReceiverId = 2, IsAccepted = true });
        await context.SaveChangesAsync();

        var controller = new FriendsController(new FriendService(context), new PassThroughBlob())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "2") }, "test"))
                }
            }
        };

        var result = await controller.GetAllFriends(2);
        var json = JsonSerializer.Serialize(((OkObjectResult)result.Result!).Value);

        Assert.Contains("Ava", json);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ava@test.com", json);
        Assert.DoesNotContain("+14155550100", json);
    }

    private sealed class PassThroughBlob : IBlobStorageService
    {
        public Task<string> UploadAsync(IFormFile file, string containerName) => Task.FromResult(file.FileName);
        public Task DeleteAsync(string blobUrl, string containerName) => Task.CompletedTask;
        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
    }
}
