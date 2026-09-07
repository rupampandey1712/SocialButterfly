using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Tests;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task QueueThenPersistThenRead_ProducesOneDurableConversationMessage()
    {
        await using var context = TestDbContextFactory.Create();
        SeedUsers(context);
        var bus = new FakeServiceBusService();
        var service = new ChatService(context, bus, new ChatMessageStore(context));
        var messageId = Guid.NewGuid();

        var queued = await service.QueueMessageAsync(1, new SendChatMessageDto
        {
            ReceiverId = 2,
            Content = "  Hello from the queue  ",
            ClientMessageId = messageId
        });

        Assert.Equal(messageId, queued.MessageId);
        var queuedMessage = Assert.IsType<ChatQueueMessage>(Assert.Single(bus.SentMessages).Message);
        Assert.Equal(ChatService.QueueName, Assert.Single(bus.SentMessages).QueueName);
        Assert.Equal("Hello from the queue", queuedMessage.Content);

        await service.PersistIncomingMessageAsync(queuedMessage);
        await service.PersistIncomingMessageAsync(queuedMessage); // Simulates Service Bus redelivery.

        var conversation = await service.GetConversationAsync(1, 2, 50);
        var message = Assert.Single(conversation);
        Assert.Equal(messageId, message.MessageId);
        Assert.Equal("Hello from the queue", message.Content);
        Assert.Equal(1, message.SenderId);
        Assert.Equal(2, message.ReceiverId);
        Assert.NotNull(message.DeliveredAt);

        var markedRead = await service.MarkConversationReadAsync(2, 1);

        Assert.Equal(1, markedRead);
        Assert.True(context.ChatMessages.Single().IsRead);
        Assert.Single(context.ChatMessages);
    }

    [Fact]
    public async Task QueueMessageAsync_RejectsAnInactiveRecipient()
    {
        await using var context = TestDbContextFactory.Create();
        context.Users.AddRange(
            new User { Id = 1, Email = "sender@test.com", Role = "User" },
            new User { Id = 2, Email = "deleted@test.com", Role = "User", IsDeleted = true });
        await context.SaveChangesAsync();
        var bus = new FakeServiceBusService();
        var service = new ChatService(context, bus, new ChatMessageStore(context));

        await Assert.ThrowsAsync<ArgumentException>(() => service.QueueMessageAsync(1, new SendChatMessageDto
        {
            ReceiverId = 2,
            Content = "Should not be queued"
        }));

        Assert.Empty(bus.SentMessages);
    }

    private static void SeedUsers(ApplicationDbContext context)
    {
        context.Users.AddRange(
            new User { Id = 1, Email = "sender@test.com", Role = "User" },
            new User { Id = 2, Email = "receiver@test.com", Role = "User" });
        context.SaveChanges();
    }

    private sealed class FakeServiceBusService : IServiceBusService
    {
        public List<(string QueueName, object Message)> SentMessages { get; } = [];

        public Task SendMessageAsync<T>(string queueName, T message, string? messageId = null)
        {
            SentMessages.Add((queueName, message!));
            return Task.CompletedTask;
        }

        public Task<T?> ReceiveMessageAsync<T>(string queueName, TimeSpan? maxWaitTime = null)
        {
            return Task.FromResult<T?>(default);
        }
    }
}
