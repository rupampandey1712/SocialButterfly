using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;

namespace SocialMediaWeb.Tests;

public class UserServiceTests
{
    [Fact]
    public async Task RegisterAsync_WithNewUser_HashesPasswordAndDefaultsRole()
    {
        await using var context = TestDbContextFactory.Create();
        var service = CreateService(context);

        await service.RegisterAsync(new UserRegistrationDto
        {
            Name = "New User",
            Email = "new@test.com",
            Password = "Password123"
        });

        var user = Assert.Single(context.Users);
        Assert.Equal("User", user.Role);
        Assert.NotEqual("Password123", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123", user.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsInvalidOperationException()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User { Email = "exists@test.com", PasswordHash = "hash" });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterAsync(new UserRegistrationDto
            {
                Email = "exists@test.com",
                Password = "Password123"
            }));
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsJwtToken()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.Add(new User
        {
            Id = 1,
            Email = "login@test.com",
            Role = "User",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123")
        });
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.LoginAsync(new UserLoginDto
        {
            Email = "login@test.com",
            Password = "Password123"
        });

        Assert.Equal(1, result.Id);
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
    }

    private static UserService CreateService(ApplicationDbContext context)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:Secret"] = "this-is-a-test-secret-with-enough-length",
                ["JWT:ValidIssuer"] = "test-issuer",
                ["JWT:ValidAudience"] = "test-audience",
                ["JWT:TokenValidityInMinutes"] = "60"
            })
            .Build();

        return new UserService(
            context,
            configuration,
            new TestWebHostEnvironment(),
            new HttpContextAccessor());
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SocialMediaWeb.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = "Development";
        public string WebRootPath { get; set; } = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
