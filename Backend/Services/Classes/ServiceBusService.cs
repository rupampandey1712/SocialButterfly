using Azure.Messaging.ServiceBus;
using SocialMediaWeb.Services.Interfaces;
using System.Text.Json;

namespace SocialMediaWeb.Services.Classes
{
    public class ServiceBusService : IServiceBusService
    {
        private readonly ServiceBusClient _client;
        private readonly ILogger<ServiceBusService> _logger;

        public ServiceBusService(ServiceBusClient client, ILogger<ServiceBusService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task SendMessageAsync<T>(string queueName, T message, string? messageId = null)
        {
            await using var sender = _client.CreateSender(queueName);

            var jsonBody = JsonSerializer.Serialize(message);
            var serviceBusMessage = new ServiceBusMessage(jsonBody)
            {
                ContentType = "application/json",
                Subject = typeof(T).Name,
                MessageId = messageId ?? Guid.NewGuid().ToString("N")
            };

            await sender.SendMessageAsync(serviceBusMessage);
            _logger.LogInformation("Sent message to queue '{Queue}': {Type}", queueName, typeof(T).Name);
        }

        public async Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null)
        {
            await using var receiver = _client.CreateReceiver(queueName);

            var message = await receiver.ReceiveMessageAsync(maxWaitTime ?? TimeSpan.FromSeconds(5));

            if (message is null)
            {
                return default;
            }

            try
            {
                var body = message.Body.ToString();
                var result = JsonSerializer.Deserialize<T>(body);
                await receiver.CompleteMessageAsync(message);
                _logger.LogInformation("Received and completed message from queue '{Queue}': {Type}", queueName, typeof(T).Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from queue '{Queue}'", queueName);
                await receiver.AbandonMessageAsync(message);
                throw;
            }
        }
    }
}
