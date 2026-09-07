using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;
using System.Text.Json;

namespace SocialMediaWeb.ChatFunctions;

/// <summary>
/// Bridges Service Bus to Durable Task. The Service Bus trigger is completed only
/// after the orchestration start has been committed to the durable task store.
/// </summary>
public sealed class ChatDeliveryFunctions
{
    private readonly IChatMessageStore _messageStore;

    public ChatDeliveryFunctions(IChatMessageStore messageStore)
    {
        _messageStore = messageStore;
    }

    [Function(nameof(StartChatDelivery))]
    public async Task StartChatDelivery(
        [ServiceBusTrigger("%ChatQueueName%", Connection = "ServiceBusConnection")] string payload,
        [DurableClient] DurableTaskClient client,
        CancellationToken cancellationToken)
    {
        var message = JsonSerializer.Deserialize<ChatQueueMessage>(payload)
            ?? throw new JsonException("The chat queue message had no payload.");

        // Reusing this instance id makes a Service Bus redelivery converge on one
        // durable workflow. The SQL unique index is the final guard after completion.
        var instanceId = $"chat-{message.MessageId:N}";
        await client.ScheduleNewOrchestrationInstanceAsync(
            new TaskName(nameof(ChatDeliveryOrchestrator)),
            message,
            new StartOrchestrationOptions { InstanceId = instanceId },
            cancellationToken);
    }

    [Function(nameof(ChatDeliveryOrchestrator))]
    public static async Task<string> ChatDeliveryOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context,
        ChatQueueMessage message)
    {
        var retryOptions = new TaskOptions(
            new TaskRetryOptions(
                new RetryPolicy(maxNumberOfAttempts: 5, firstRetryInterval: TimeSpan.FromSeconds(3))));

        return await context.CallActivityAsync<string>(
            new TaskName(nameof(PersistChatMessageActivity)),
            message,
            retryOptions);
    }

    [Function(nameof(PersistChatMessageActivity))]
    public async Task<string> PersistChatMessageActivity(
        [ActivityTrigger] ChatQueueMessage message,
        CancellationToken cancellationToken)
    {
        await _messageStore.PersistAsync(message, cancellationToken);
        return "Persisted";
    }
}
