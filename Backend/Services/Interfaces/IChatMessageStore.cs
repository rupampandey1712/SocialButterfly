using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces;

/// <summary>
/// Durable storage boundary shared by the API and the Durable Functions activity.
/// </summary>
public interface IChatMessageStore
{
    Task PersistAsync(ChatQueueMessage message, CancellationToken cancellationToken = default);
}
