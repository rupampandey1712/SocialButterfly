namespace SocialMediaWeb.Services.Interfaces
{
    public interface INotificationPublisher
    {
        /// <summary>
        /// Publishes an activity event to Service Bus. Failures are logged, never thrown:
        /// a notification outage must not fail the like/comment/post that raised it.
        /// </summary>
        Task PublishAsync(string type, int actorId, int? recipientId = null, int? postId = null, string? text = null);
    }
}
