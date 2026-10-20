using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;
using System.Text.Json;

namespace SocialMediaWeb.ChatFunctions;

/// <summary>
/// Background work for accounts and stories:
/// - delivers verification / login / reset codes queued by the API (Service Bus trigger);
/// - removes expired 24-hour stories and their blobs (timer trigger).
/// </summary>
public sealed class AccountFunctions
{
    private readonly IVerificationSender _sender;
    private readonly IStoryService _stories;
    private readonly ILogger<AccountFunctions> _logger;

    public AccountFunctions(IVerificationSender sender, IStoryService stories, ILogger<AccountFunctions> logger)
    {
        _sender = sender;
        _stories = stories;
        _logger = logger;
    }

    [Function(nameof(SendVerificationCode))]
    public async Task SendVerificationCode(
        [ServiceBusTrigger("%VerificationQueueName%", Connection = "ServiceBusConnection")] string payload,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<VerificationCodeMessage>(payload)
            ?? throw new JsonException("The verification queue message had no payload.");

        // Codes are only useful for 10 minutes; don't deliver one that already expired in the queue.
        if (message.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Dropped expired {Purpose} code {EventId}", message.Purpose, message.EventId);
            return;
        }

        await _sender.SendAsync(message, cancellationToken);
    }

    [Function(nameof(CleanupExpiredStories))]
    public async Task CleanupExpiredStories(
        [TimerTrigger("0 */15 * * * *", RunOnStartup = true)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var removed = await _stories.CleanupExpiredAsync(cancellationToken);
        if (removed > 0)
        {
            _logger.LogInformation("Removed {Count} expired stories", removed);
        }
    }
}
