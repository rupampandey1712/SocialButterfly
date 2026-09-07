namespace SocialMediaWeb.Dtos;

public sealed class ChatMessageDto
{
    public int Id { get; init; }
    public Guid MessageId { get; init; }
    public int SenderId { get; init; }
    public int ReceiverId { get; init; }
    public string Content { get; init; } = string.Empty;
    public DateTime SentAt { get; init; }
    public DateTime? DeliveredAt { get; init; }
    public bool IsRead { get; init; }
}
