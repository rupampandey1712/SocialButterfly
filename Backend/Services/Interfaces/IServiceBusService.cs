namespace SocialMediaWeb.Services.Interfaces
{
    public interface IServiceBusService
    {
        /// <summary>
        /// Sends a serialized message to the specified Service Bus queue.
        /// </summary>
        Task SendMessageAsync<T>(string queueName, T message, string? messageId = null);

        /// <summary>
        /// Receives and deserializes a single message from the specified queue.
        /// Returns null if no message is available within the timeout.
        /// </summary>
        Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null);
    }
}
