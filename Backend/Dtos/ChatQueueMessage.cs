namespace SocialMediaWeb.Dtos;

/// <summary>
/// The small, versionable payload placed on the Service Bus queue. Keep persistence
/// concerns out of this contract so the consumer can be moved to a separate worker later.
/// </summary>
public sealed record ChatQueueMessage(
    Guid MessageId,
    int SenderId,
    int ReceiverId,
    string Content,
    DateTime SentAt);
