using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Security;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public class CsrfProtectionTests
{
    private const string Token = "abc123token";

    [Fact]
    public async Task PostWithMatchingTokenFromAllowedOrigin_IsAllowed()
    {
        var (status, reachedApp) = await Run("POST", origin: "http://localhost:5173", cookie: Token, header: Token);
        Assert.True(reachedApp);
        Assert.Equal(200, status);
    }

    [Theory]
    [InlineData(null)]                // forged form: no header at all
    [InlineData("wrong-token")]       // guessed token
    public async Task PostWithoutValidToken_IsRejected(string? header)
    {
        var (status, reachedApp) = await Run("POST", origin: "http://localhost:5173", cookie: Token, header: header);
        Assert.False(reachedApp);
        Assert.Equal(403, status);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("null")]               // sandboxed iframe / file:// pages
    public async Task PostFromForeignOrigin_IsRejected_EvenWithToken(string origin)
    {
        var (status, reachedApp) = await Run("DELETE", origin: origin, cookie: Token, header: Token);
        Assert.False(reachedApp);
        Assert.Equal(403, status);
    }

    [Fact]
    public async Task BearerHeaderClients_SkipTokenCheck()
    {
        var (status, reachedApp) = await Run("POST", origin: null, cookie: null, header: null, bearer: true);
        Assert.True(reachedApp);
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task SafeMethods_PassAndReceiveCsrfCookie()
    {
        var context = NewContext("GET", origin: null, cookie: null, header: null);
        var reachedApp = false;
        var middleware = new CsrfProtectionMiddleware(_ => { reachedApp = true; return Task.CompletedTask; }, Config(), new DevEnvironment());

        await middleware.InvokeAsync(context);

        Assert.True(reachedApp);
        Assert.Contains(context.Response.Headers.SetCookie, c => c!.StartsWith(CsrfProtectionMiddleware.CookieName + "="));
    }

    private static async Task<(int Status, bool ReachedApp)> Run(string method, string? origin, string? cookie, string? header, bool bearer = false)
    {
        var context = NewContext(method, origin, cookie, header);
        if (bearer) context.Request.Headers.Authorization = "Bearer some.jwt.token";
        var reachedApp = false;
        var middleware = new CsrfProtectionMiddleware(_ => { reachedApp = true; return Task.CompletedTask; }, Config(), new DevEnvironment());

        await middleware.InvokeAsync(context);
        return (context.Response.StatusCode, reachedApp);
    }

    private static DefaultHttpContext NewContext(string method, string? origin, string? cookie, string? header)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("localhost", 8080);
        context.Request.Path = "/api/Post";
        if (origin != null) context.Request.Headers.Origin = origin;
        if (cookie != null) context.Request.Headers.Cookie = $"{CsrfProtectionMiddleware.CookieName}={cookie}";
        if (header != null) context.Request.Headers[CsrfProtectionMiddleware.HeaderName] = header;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Security:AllowedOrigins:0"] = "http://localhost:5173" })
        .Build();

    private sealed class DevEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

public class InputHardeningTests
{
    [Theory]
    [InlineData("ava", "%ava%")]
    [InlineData("100%", "%100[%]%")]
    [InlineData("a_b", "%a[_]b%")]
    [InlineData("[x]", "%[[]x]%")]
    [InlineData("'; DROP TABLE Users;--", "%'; DROP TABLE Users;--%")] // stays a plain parameter value
    public void SearchText_EscapesLikeWildcards(string input, string expected)
    {
        Assert.Equal(expected, SearchText.Contains(input));
    }

    [Fact]
    public void SearchText_CapsLength()
    {
        Assert.Equal(SearchText.MaximumLength + 2, SearchText.Contains(new string('a', 5000)).Length);
    }

    [Theory]
    [InlineData("../../etc/passwd.jpg")]
    [InlineData("..\\..\\web.config.png")]
    [InlineData("<script>alert(1)</script>.jpg")]
    [InlineData("photo.jpg‮gnp.exe")]
    public void SafeBlobName_HasNoPathOrMarkupCharacters(string fileName)
    {
        var blobName = BlobStorageService.SafeBlobName(fileName);

        Assert.DoesNotContain("/", blobName);
        Assert.DoesNotContain("\\", blobName);
        Assert.DoesNotContain("..", blobName);
        Assert.DoesNotContain("<", blobName);
        Assert.Matches("^[A-Za-z0-9_-]+(\\.[a-z0-9]{1,5})?$", blobName);
    }

    [Theory]
    [InlineData("song.mp3", "audio/mpeg")]
    [InlineData("clip.mp4", "video/mp4")]
    [InlineData("pic.jpeg", "image/jpeg")]
    [InlineData("noext", "application/octet-stream")]
    public void ContentType_ComesFromExtension(string blobName, string expected)
    {
        Assert.Equal(expected, BlobStorageService.ContentTypeFor(blobName));
    }

    [Fact]
    public async Task LoginCode_ForUnknownAccount_LooksLikeSuccess_AndSendsNothing()
    {
        await using var context = TestDbContextFactory.Create();
        var bus = new CountingBus();
        var verification = new VerificationService(context, bus, new NullUserService(), new DevEnv(), new ConfigurationBuilder().Build());

        var response = await verification.SendCodeAsync(new SendCodeRequestDto { Target = "ghost@test.com", Purpose = VerificationPurposes.Login });

        Assert.Equal(VerificationChannels.Email, response.Channel);
        Assert.Null(response.DevCode);
        Assert.Equal(0, bus.Sent);
        Assert.Empty(context.VerificationCodes);
    }

    private sealed class CountingBus : IServiceBusService
    {
        public int Sent { get; private set; }
        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null) { Sent++; return Task.CompletedTask; }
        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null) => Task.FromResult(default(T));
    }

    private sealed class DevEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class NullUserService : IUserService
    {
        public Task RegisterAsync(UserRegistrationDto registrationDto) => throw new NotSupportedException();
        public Task<UserResponseDto> LoginAsync(UserLoginDto loginDto) => throw new NotSupportedException();
        public Task<SocialMediaWeb.Models.User> UpdateUserAsync(UserUpdateDto userUpdateDto) => throw new NotSupportedException();
        public Task<SocialMediaWeb.Models.User> FindById(int id) => throw new NotSupportedException();
        public Task<UserProfileDto> GetProfileAsync(int userId, int? viewerId) => throw new NotSupportedException();
        public Task<RegisterResponseDto> RegisterWithResultAsync(UserRegistrationDto registrationDto) => throw new NotSupportedException();
        public Task<SocialMediaWeb.Models.User?> FindByIdentifierAsync(string identifier) => throw new NotSupportedException();
        public UserResponseDto ToLoginResponse(SocialMediaWeb.Models.User user) => throw new NotSupportedException();
        public Task<AvailabilityDto> CheckAvailabilityAsync(string? username, string? email, string? phone, string? name) => throw new NotSupportedException();
        public Task<UserSettingsDto> GetSettingsAsync(int userId) => throw new NotSupportedException();
        public Task<UserSettingsDto> UpdateSettingsAsync(int userId, UpdateSettingsDto dto) => throw new NotSupportedException();
        public Task ChangePasswordAsync(int userId, ChangePasswordDto dto) => throw new NotSupportedException();
        public Task<List<ResponseDto>> GetUserAsync() => throw new NotSupportedException();
        public IQueryable<UserDto> Search(UserDto dto) => throw new NotSupportedException();
    }
}
