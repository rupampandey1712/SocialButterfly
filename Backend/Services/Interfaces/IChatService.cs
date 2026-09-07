using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces;

public interface IChatService
{
    Task<QueuedChatMessageDto> QueueMessageAsync(int senderId, SendChatMessageDto request, CancellationToken cancellationToken = default);
    Task PersistIncomingMessageAsync(ChatQueueMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageDto>> GetConversationAsync(int userId, int otherUserId, int limit, CancellationToken cancellationToken = default);
    Task<int> MarkConversationReadAsync(int userId, int otherUserId, CancellationToken cancellationToken = default);
}
