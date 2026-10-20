using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class IdentityRulesTests
{
    [Theory]
    [InlineData("+91 98765 43210", "+919876543210")]
    [InlineData("0044 20 7946 0958", "+442079460958")]
    [InlineData("+1 (415) 555-0100", "+14155550100")]
    public void NormalizePhone_AcceptsCommonFormats(string input, string expected)
    {
        Assert.Equal(expected, IdentityRules.NormalizePhone(input));
    }

    [Theory]
    [InlineData("9876543210")]
    [InlineData("+12")]
    [InlineData("+91 98765 abc")]
    public void NormalizePhone_RejectsMissingCountryCodeOrJunk(string input)
    {
        Assert.Null(IdentityRules.NormalizePhone(input));
    }

    [Theory]
    [InlineData("Ava.Martin", "ava.martin")]
    [InlineData("@leo_park", "leo_park")]
    public void NormalizeUsername_LowercasesAndStripsAt(string input, string expected)
    {
        Assert.Equal(expected, IdentityRules.NormalizeUsername(input));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData(".dotstart")]
    public void NormalizeUsername_RejectsInvalid(string input)
    {
        Assert.Null(IdentityRules.NormalizeUsername(input));
    }

    [Fact]
    public void NormalizeEmail_RejectsMalformed()
    {
        Assert.Equal("ava@test.com", IdentityRules.NormalizeEmail(" Ava@Test.com "));
        Assert.Null(IdentityRules.NormalizeEmail("ava@localhost"));
        Assert.Null(IdentityRules.NormalizeEmail("not-an-email"));
    }
}

public class AccountFeatureTests
{
    [Fact]
    public async Task Register_WithPhoneOnly_GeneratesUsername_AndLoginWorksWithPhoneAndUsername()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);

        var result = await users.RegisterWithResultAsync(new UserRegistrationDto
        {
            Name = "Ava Martin",
            PhoneNumber = "+91 98765 43210",
            Password = "Password123"
        });

        Assert.Equal("ava.martin", result.Username);
        Assert.Equal("+919876543210", result.VerificationTarget);

        var byPhone = await users.LoginAsync(new UserLoginDto { Identifier = "+919876543210", Password = "Password123" });
        var byUsername = await users.LoginAsync(new UserLoginDto { Identifier = "@Ava.Martin", Password = "Password123" });
        Assert.Equal(result.Id, byPhone.Id);
        Assert.Equal(result.Id, byUsername.Id);
    }

    [Fact]
    public async Task Register_WithTakenUsername_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Username = "taken", Email = "a@test.com" });
        await context.SaveChangesAsync();
        var users = CreateUserService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => users.RegisterWithResultAsync(new UserRegistrationDto
        {
            Username = "Taken",
            Email = "b@test.com",
            Password = "Password123"
        }));
    }

    [Fact]
    public async Task LegacyUpdate_CannotClaimAnotherMembersEmail_OrSetInvalidEmail()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Email = "victim@test.com", EmailConfirmed = true },
            new User { Id = 2, Email = "attacker@test.com", EmailConfirmed = true });
        await context.SaveChangesAsync();
        var users = CreateUserService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => users.UpdateUserAsync(new UserUpdateDto { Id = 2, Email = "Victim@Test.com" }));
        await Assert.ThrowsAsync<ArgumentException>(() => users.UpdateUserAsync(new UserUpdateDto { Id = 2, Email = "not-an-email" }));

        var updated = await users.UpdateUserAsync(new UserUpdateDto { Id = 2, Email = "new@test.com" });
        Assert.Equal("new@test.com", updated.Email);
        Assert.False(updated.EmailConfirmed);
    }

    [Theory]
    [InlineData("evil.html", "text/html")]
    [InlineData("photo.html", "image/png")]   // lies about its type: stored as .html would be served as HTML
    [InlineData("logo.svg", "image/svg+xml")] // SVG can carry script
    public async Task ProfilePhoto_MustBeARealImageType_OnRegisterAndSettings(string fileName, string contentType)
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);
        var file = new FormFile(new MemoryStream(new byte[] { 1, 2, 3 }), 0, 3, "ImageFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };

        await Assert.ThrowsAsync<ArgumentException>(() => users.RegisterWithResultAsync(new UserRegistrationDto
        {
            Email = "pic@test.com", Password = "Password123", ImageFile = file
        }));

        var created = await users.RegisterWithResultAsync(new UserRegistrationDto { Email = "pic@test.com", Password = "Password123" });
        await Assert.ThrowsAsync<ArgumentException>(() => users.UpdateSettingsAsync(created.Id, new UpdateSettingsDto { ImageFile = file }));
        await Assert.ThrowsAsync<ArgumentException>(() => users.UpdateUserAsync(new UserUpdateDto { Id = created.Id, ImageFile = file }));
    }

    [Fact]
    public async Task Register_WithShortPassword_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);

        await Assert.ThrowsAsync<ArgumentException>(() => users.RegisterWithResultAsync(new UserRegistrationDto
        {
            Email = "b@test.com",
            Password = "short"
        }));
    }

    [Fact]
    public async Task VerificationCode_ConfirmsEmail_AndRejectsWrongCode()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);
        await users.RegisterWithResultAsync(new UserRegistrationDto { Email = "ava@test.com", Password = "Password123" });
        var bus = new RecordingServiceBus();
        var verification = CreateVerificationService(context, users, bus);

        var sent = await verification.SendCodeAsync(new SendCodeRequestDto { Target = "AVA@test.com", Purpose = VerificationPurposes.Verify });

        Assert.Equal(VerificationChannels.Email, sent.Channel);
        Assert.NotNull(sent.DevCode);
        var queued = Assert.Single(bus.Messages);
        Assert.Equal(VerificationService.QueueName, queued.Queue);

        var wrong = sent.DevCode == "000000" ? "111111" : "000000";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => verification.ConfirmCodeAsync(
            new ConfirmCodeRequestDto { Target = "ava@test.com", Purpose = VerificationPurposes.Verify, Code = wrong }));

        var session = await verification.ConfirmCodeAsync(
            new ConfirmCodeRequestDto { Target = "ava@test.com", Purpose = VerificationPurposes.Verify, Code = sent.DevCode });

        Assert.Null(session);
        Assert.True(Assert.Single(context.Users).EmailConfirmed);
    }

    [Fact]
    public async Task VerificationCode_LoginPurpose_ReturnsSession_AndCodeIsSingleUse()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);
        await users.RegisterWithResultAsync(new UserRegistrationDto { PhoneNumber = "+14155550100", Password = "Password123" });
        var verification = CreateVerificationService(context, users, new RecordingServiceBus());

        var sent = await verification.SendCodeAsync(new SendCodeRequestDto { Target = "+1 415 555 0100", Purpose = VerificationPurposes.Login });
        var request = new ConfirmCodeRequestDto { Target = "+14155550100", Purpose = VerificationPurposes.Login, Code = sent.DevCode };

        var session = await verification.ConfirmCodeAsync(request);

        Assert.False(string.IsNullOrWhiteSpace(session?.Token));
        Assert.True(Assert.Single(context.Users).PhoneConfirmed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => verification.ConfirmCodeAsync(request));
    }

    [Fact]
    public async Task VerificationCode_ResendTooSoon_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);
        await users.RegisterWithResultAsync(new UserRegistrationDto { Email = "ava@test.com", Password = "Password123" });
        var verification = CreateVerificationService(context, users, new RecordingServiceBus());

        await verification.SendCodeAsync(new SendCodeRequestDto { Target = "ava@test.com", Purpose = VerificationPurposes.Reset });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            verification.SendCodeAsync(new SendCodeRequestDto { Target = "ava@test.com", Purpose = VerificationPurposes.Reset }));
    }

    [Fact]
    public async Task ResetCode_ChangesPassword()
    {
        await using var context = TestDbContextFactory.Create();
        var users = CreateUserService(context);
        await users.RegisterWithResultAsync(new UserRegistrationDto { Email = "ava@test.com", Password = "Password123" });
        var verification = CreateVerificationService(context, users, new RecordingServiceBus());
        var sent = await verification.SendCodeAsync(new SendCodeRequestDto { Target = "ava@test.com", Purpose = VerificationPurposes.Reset });

        await verification.ConfirmCodeAsync(new ConfirmCodeRequestDto
        {
            Target = "ava@test.com",
            Purpose = VerificationPurposes.Reset,
            Code = sent.DevCode,
            NewPassword = "NewPassword456"
        });

        var session = await users.LoginAsync(new UserLoginDto { Identifier = "ava@test.com", Password = "NewPassword456" });
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
    }

    [Fact]
    public async Task Follow_TogglesAndListsBothDirections()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Name = "Ava", Username = "ava" },
            new User { Id = 2, Name = "Leo", Username = "leo" });
        await context.SaveChangesAsync();
        var follows = new FollowService(context);

        var followed = await follows.ToggleFollowAsync(followerId: 2, targetId: 1);
        Assert.True(followed.IsFollowing);
        Assert.Equal(1, followed.FollowersCount);

        var followers = await follows.GetFollowersAsync(1, viewerId: 1);
        Assert.Equal("leo", Assert.Single(followers).Username);
        Assert.True(followers[0].FollowsYou);
        Assert.False(followers[0].IsFollowing);

        var following = await follows.GetFollowingAsync(2, viewerId: 2);
        Assert.Equal("ava", Assert.Single(following).Username);

        var unfollowed = await follows.ToggleFollowAsync(followerId: 2, targetId: 1);
        Assert.False(unfollowed.IsFollowing);
        Assert.Empty(context.Follows);
    }

    [Fact]
    public async Task Follow_Self_Throws()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Id = 1 });
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => new FollowService(context).ToggleFollowAsync(1, 1));
    }

    [Fact]
    public async Task StoryFeed_ShowsFollowedAuthors_TracksSeen_AndHidesExpired()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Name = "Ava" },
            new User { Id = 2, Name = "Leo" },
            new User { Id = 3, Name = "Stranger" });
        context.Follows.Add(new Follow { FollowerId = 2, FollowingId = 1 });
        var now = DateTime.UtcNow;
        context.Stories.AddRange(
            new Story { Id = 10, UserId = 1, MediaPath = "a.jpg", CreatedAt = now.AddHours(-1), ExpiresAt = now.AddHours(23) },
            new Story { Id = 11, UserId = 1, MediaPath = "b.jpg", CreatedAt = now.AddHours(-30), ExpiresAt = now.AddHours(-6) },
            new Story { Id = 12, UserId = 3, MediaPath = "c.jpg", CreatedAt = now, ExpiresAt = now.AddHours(24) });
        await context.SaveChangesAsync();
        var blob = new RecordingBlobStorage();
        var stories = new StoryService(context, blob);

        var feed = await stories.GetFeedAsync(viewerId: 2);
        var group = Assert.Single(feed);
        Assert.Equal(1, group.User.Id);
        Assert.Equal(10, Assert.Single(group.Stories).Id);
        Assert.True(group.HasUnseen);

        await stories.MarkViewedAsync(10, viewerId: 2);
        await stories.MarkViewedAsync(10, viewerId: 2);
        Assert.Single(context.StoryViews);
        Assert.False((await stories.GetFeedAsync(viewerId: 2))[0].HasUnseen);

        var removed = await stories.CleanupExpiredAsync();
        Assert.Equal(1, removed);
        Assert.Equal(new[] { "b.jpg" }, blob.Deleted);
    }

    [Fact]
    public async Task StoryViewers_OnlyForAuthor()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(new User { Id = 1, Name = "Ava" }, new User { Id = 2, Name = "Leo" });
        context.Stories.Add(new Story { Id = 10, UserId = 1, MediaPath = "a.jpg" });
        context.StoryViews.Add(new StoryView { StoryId = 10, ViewerId = 2 });
        await context.SaveChangesAsync();
        var stories = new StoryService(context, new RecordingBlobStorage());

        Assert.Equal("Leo", Assert.Single(await stories.GetViewersAsync(10, ownerId: 1)).User.Name);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => stories.GetViewersAsync(10, ownerId: 2));
    }

    [Fact]
    public async Task MutedRecipients_DoNotGetNotifications()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(new User { Id = 1, MuteFollowNotifications = true }, new User { Id = 2 });
        await context.SaveChangesAsync();
        var store = new NotificationStore(context);

        await store.ProcessAsync(new NotificationEvent(Guid.NewGuid(), NotificationTypes.UserFollowed, 2, 1, null, null, DateTime.UtcNow));

        Assert.Empty(context.Notifications);
    }

    private static UserService CreateUserService(ApplicationDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = "this-is-a-test-secret-with-enough-length",
                ["JWT:ValidIssuer"] = "test-issuer",
                ["JWT:ValidAudience"] = "test-audience"
            })
            .Build();
        return new UserService(context, configuration, new RecordingBlobStorage(), new HttpContextAccessor());
    }

    private static VerificationService CreateVerificationService(ApplicationDbContext context, IUserService users, IServiceBusService bus)
    {
        return new VerificationService(context, bus, users, new DevelopmentEnvironment(), new ConfigurationBuilder().Build());
    }

    private sealed class RecordingServiceBus : IServiceBusService
    {
        public List<(string Queue, object? Message)> Messages { get; } = new();

        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null)
        {
            Messages.Add((queueName, message));
            return Task.CompletedTask;
        }

        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null) => Task.FromResult(default(T));
    }

    private sealed class RecordingBlobStorage : IBlobStorageService
    {
        public List<string> Deleted { get; } = new();
        public Task<string> UploadAsync(IFormFile file, string containerName) => Task.FromResult(file.FileName);

        public Task DeleteAsync(string blobUrl, string containerName)
        {
            Deleted.Add(blobUrl);
            return Task.CompletedTask;
        }

        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
    }

    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
