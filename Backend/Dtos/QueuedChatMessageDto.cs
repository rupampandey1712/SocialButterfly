namespace SocialMediaWeb.Dtos;

public sealed class QueuedChatMessageDto
{
    public Guid MessageId { get; init; }
    public string Status { get; init; } = "Queued";
}
