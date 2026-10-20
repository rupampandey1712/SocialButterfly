using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SocialMediaWeb.Controllers;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class SecurityTests
{
    private static readonly Type[] Controllers = typeof(AccountController).Assembly
        .GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

    private static IEnumerable<MethodInfo> Actions(Type controller) => controller
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any());

    /// <summary>
    /// The app uses a deny-by-default fallback policy, so this list is the complete set of
    /// endpoints a signed-out visitor can call. Adding to it should be a deliberate decision.
    /// </summary>
    [Fact]
    public void OnlySignInEndpoints_AllowAnonymousAccess()
    {
        var anonymous = Controllers
            .SelectMany(c => Actions(c).Select(a => (Controller: c, Action: a)))
            .Where(x => x.Action.GetCustomAttribute<AllowAnonymousAttribute>() != null ||
                        x.Controller.GetCustomAttribute<AllowAnonymousAttribute>() != null)
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(new[]
        {
            "AccountController.Availability",
            "AccountController.ConfirmCode",
            "AccountController.Login",
            "AccountController.Logout",
            "AccountController.Register",
            "AccountController.SendCode",
            "AccountController.Session"
        }, anonymous);
    }

    [Fact]
    public void AdminController_RequiresAdminRole()
    {
        var authorize = typeof(AdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal("Admin", authorize!.Roles);
    }

    /// <summary>Who is calling must come from the JWT, never from a client-supplied id.</summary>
    [Fact]
    public void NoAction_AcceptsCallerIdentityFromTheClient()
    {
        var callerParameterNames = new[] { "viewerId", "followerId", "currentUserId" };
        var offenders = Controllers
            .SelectMany(c => Actions(c).SelectMany(a => a.GetParameters()
                .Where(p => callerParameterNames.Contains(p.Name, StringComparer.OrdinalIgnoreCase) ||
                            (p.Name == "userId" && p.GetCustomAttribute<FromQueryAttribute>() != null))
                .Select(p => $"{c.Name}.{a.Name}({p.Name})")))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public async Task Register_IgnoresRequestedAdminRole()
    {
        await using var context = TestDbContextFactory.Create();
        var users = new UserService(context, JwtConfig(), new NullBlobStorage(), new HttpContextAccessor());

        await users.RegisterWithResultAsync(new UserRegistrationDto
        {
            Email = "sneaky@test.com",
            Password = "Password123",
            Role = "Admin"
        });

        Assert.Equal("User", Assert.Single(context.Users).Role);
    }

    [Fact]
    public async Task AdminSeeder_CreatesAdmin_AndPromotesExistingAccount()
    {
        await using var context = TestDbContextFactory.Create();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:Email"] = "Boss@Test.com",
            ["Admin:Password"] = "VeryStrong123"
        }).Build();

        await AdminSeeder.EnsureAdminAsync(context, config, NullLogger.Instance);
        var admin = Assert.Single(context.Users);
        Assert.Equal("Admin", admin.Role);
        Assert.Equal("boss@test.com", admin.Email);
        Assert.True(BCrypt.Net.BCrypt.Verify("VeryStrong123", admin.PasswordHash));

        admin.Role = "User";
        await context.SaveChangesAsync();
        await AdminSeeder.EnsureAdminAsync(context, config, NullLogger.Instance);
        Assert.Equal("Admin", Assert.Single(context.Users).Role);
    }

    [Fact]
    public async Task AdminSeeder_WithoutConfiguration_DoesNothing()
    {
        await using var context = TestDbContextFactory.Create();

        await AdminSeeder.EnsureAdminAsync(context, new ConfigurationBuilder().Build(), NullLogger.Instance);

        Assert.Empty(context.Users);
    }

    private static IConfiguration JwtConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JWT:Secret"] = "this-is-a-test-secret-with-enough-length",
            ["JWT:ValidIssuer"] = "test-issuer",
            ["JWT:ValidAudience"] = "test-audience"
        })
        .Build();

    private sealed class NullBlobStorage : IBlobStorageService
    {
        public Task<string> UploadAsync(IFormFile file, string containerName) => Task.FromResult(file.FileName);
        public Task DeleteAsync(string blobUrl, string containerName) => Task.CompletedTask;
        public string GetBlobUrl(string containerName, string blobName) => blobName;
        public string? ToPublicUrl(string? blobUrl) => blobUrl;
    }
}
