using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;
using System.Text.Json;

namespace SocialMediaWeb.ChatFunctions;

/// <summary>
/// Consumes Instagram-style activity events (likes, comments, new posts, friend
/// requests) and fans them out into per-recipient notification rows. A thrown
/// exception abandons the message, so Service Bus retries up to MaxDeliveryCount
/// before dead-lettering; <see cref="INotificationStore"/> makes retries idempotent.
/// </summary>
public sealed class NotificationFunctions
{
    private readonly INotificationStore _store;
    private readonly ILogger<NotificationFunctions> _logger;

    public NotificationFunctions(INotificationStore store, ILogger<NotificationFunctions> logger)
    {
        _store = store;
        _logger = logger;
    }

    [Function(nameof(ProcessPostNotification))]
    public Task ProcessPostNotification(
        [ServiceBusTrigger("%PostNotificationsQueueName%", Connection = "ServiceBusConnection")] string payload,
        CancellationToken cancellationToken)
    {
        return ProcessAsync(payload, cancellationToken);
    }

    [Function(nameof(ProcessFriendRequestNotification))]
    public Task ProcessFriendRequestNotification(
        [ServiceBusTrigger("%FriendRequestsQueueName%", Connection = "ServiceBusConnection")] string payload,
        CancellationToken cancellationToken)
    {
        return ProcessAsync(payload, cancellationToken);
    }

    private async Task ProcessAsync(string payload, CancellationToken cancellationToken)
    {
        var notification = JsonSerializer.Deserialize<NotificationEvent>(payload)
            ?? throw new JsonException("The notification queue message had no payload.");

        await _store.ProcessAsync(notification, cancellationToken);
        _logger.LogInformation("Processed {Type} notification {EventId}", notification.Type, notification.EventId);
    }
}
