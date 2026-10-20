using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class NotificationPublisher : INotificationPublisher
    {
        private const int MaximumTextLength = 300;
        private readonly IServiceBusService _serviceBus;
        private readonly ILogger<NotificationPublisher>? _logger;

        public NotificationPublisher(IServiceBusService serviceBus, ILogger<NotificationPublisher>? logger = null)
        {
            _serviceBus = serviceBus;
            _logger = logger;
        }

        public async Task PublishAsync(string type, int actorId, int? recipientId = null, int? postId = null, string? text = null)
        {
            var notification = new NotificationEvent(
                Guid.NewGuid(),
                type,
                actorId,
                recipientId,
                postId,
                text?.Length > MaximumTextLength ? text[..MaximumTextLength] : text,
                DateTime.UtcNow);

            try
            {
                await _serviceBus.SendMessageAsync(
                    NotificationQueues.For(type),
                    notification,
                    notification.EventId.ToString("N"));
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not publish {Type} notification from user {ActorId}", type, actorId);
            }
        }
    }
}
