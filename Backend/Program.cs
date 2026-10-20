using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SocialMediaWeb.Models;
using SocialMediaWeb.Security;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;
using Swashbuckle.AspNetCore.Filters;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (OpenTelemetry, health checks, resilience)
builder.AddServiceDefaults();

builder.WebHost.UseWebRoot("wwwroot");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 80 * 1024 * 1024);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 80 * 1024 * 1024;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Con")
        ?? builder.Configuration.GetConnectionString("SocialMedia")
        ?? throw new InvalidOperationException("Connection string 'Con' or 'SocialMedia' is missing.")));

builder.Services.AddHttpContextAccessor();

// Azure Blob Storage (Azurite in dev, Azure in prod)
builder.AddAzureBlobClient("blobs");

// Azure Service Bus (Emulator in dev, Azure in prod)
builder.AddAzureServiceBusClient("servicebus");

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISocialMediaPost, SocialMedia>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddScoped<ILikeService, LikeService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddSingleton<IGenAiClientService, GenAiClientService>();
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddScoped<IServiceBusService, ServiceBusService>();
builder.Services.AddScoped<IChatMessageStore, ChatMessageStore>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<INotificationStore, NotificationStore>();
builder.Services.AddScoped<IVerificationService, VerificationService>();
builder.Services.AddScoped<IFollowService, FollowService>();
builder.Services.AddScoped<IStoryService, StoryService>();

// Bot defence for sign-in endpoints (risk scoring + proof-of-work challenges).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<BotDefense>();
builder.Services.AddSingleton<ProofOfWork>();
builder.Services.AddSingleton<BotGate>();

// AI image studio (ComfyUI). Generation runs in the Functions worker; the API queues jobs.
builder.Services.AddHttpClient(ComfyUiClient.HttpClientName, client => ComfyUiClient.Configure(client, builder.Configuration));
builder.Services.AddSingleton<ComfyUiClient>();
builder.Services.AddScoped<IImageGenerationService, ImageGenerationService>();

// Behind a reverse proxy / load balancer, trust X-Forwarded-For only from known proxies so
// rate limits and bot scores use the real client IP (and clients can't spoof it).
builder.Services.Configure<Microsoft.AspNetCore.Builder.ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                               Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    foreach (var proxy in builder.Configuration.GetSection("Security:TrustedProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (System.Net.IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
    }
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Description = "Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey
    });

    options.OperationFilter<SecurityRequirementsOperationFilter>();
});


builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})

// Adding Jwt Bearer
.AddJwtBearer(options =>
{
    // Browsers authenticate with the HttpOnly session cookie; API tools may still send a Bearer header.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            if (string.IsNullOrEmpty(context.Token))
            {
                context.Token = context.Request.Cookies[AuthCookies.SessionCookie];
            }
            return Task.CompletedTask;
        }
    };
    options.SaveToken = true;
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new TokenValidationParameters()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,

        ValidAudience = builder.Configuration["JWT:ValidAudience"]
            ?? throw new InvalidOperationException("JWT:ValidAudience is missing."),
        ValidIssuer = builder.Configuration["JWT:ValidIssuer"]
            ?? throw new InvalidOperationException("JWT:ValidIssuer is missing."),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["JWT:Secret"]
                ?? throw new InvalidOperationException("JWT:Secret is missing.")))
    };
});

// Secure by default: every endpoint needs a valid JWT unless it opts out with [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Slow down password guessing and one-time-code brute forcing (per client IP).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        await context.HttpContext.Response.WriteAsync("Too many attempts. Please wait a minute and try again.", token);
    };

    static string ClientKey(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    options.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("ai", http => RateLimitPartition.GetFixedWindowLimiter(http.User.GetUserId()?.ToString() ?? ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("codes", http => RateLimitPartition.GetFixedWindowLimiter(ClientKey(http),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 8, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Only our own frontend origins may call the API from a browser, with credentials (cookies).
var allowedOrigins = AllowedOrigins.From(builder.Configuration).ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactJsDomain", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowCredentials()
        .WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Content-Type", CsrfProtectionMiddleware.HeaderName, ProofOfWork.HeaderName));
});

builder.Services.AddEndpointsApiExplorer();

var jwtSecret = builder.Configuration["JWT:Secret"] ?? string.Empty;
if (!builder.Environment.IsDevelopment() &&
    (jwtSecret.Length < 32 || jwtSecret == "JWTRefreshTokenHIGHsecuredPasswordVVVp1OH7Xzyr"))
{
    // The sample secret is public: anyone could forge login tokens with it.
    throw new InvalidOperationException("Set JWT:Secret (JWT_SECRET) to a private random value of at least 32 characters.");
}

var app = builder.Build();

app.UseForwardedHeaders();

// Never show stack traces or exception details to clients (Docker runs in Development).
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsync("{\"title\":\"Something went wrong. Please try again.\",\"status\":500}");
}));

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrations"))
{
    using var migrationScope = app.Services.CreateScope();
    await migrationScope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>()
        .Database
        .MigrateAsync();
}

{
    using var seedScope = app.Services.CreateScope();
    await AdminSeeder.EnsureAdminAsync(
        seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
        app.Configuration,
        app.Logger);
}

Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "Images"));
Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath, "posts"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("ReactJsDomain");
app.UseMiddleware<CsrfProtectionMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Map Aspire default health check endpoints
app.MapDefaultEndpoints();

app.MapFallback(async context =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api")
        || path.StartsWithSegments("/Images")
        || path.StartsWithSegments("/posts")
        || path.StartsWithSegments("/swagger"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    var indexPath = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (!File.Exists(indexPath))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(indexPath);
}).AllowAnonymous();

app.Run();
