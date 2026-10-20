namespace SocialMediaWeb.Dtos;

/// <summary>
/// Activity event placed on Service Bus. The Functions worker turns it into
/// per-recipient <see cref="Models.Notification"/> rows, so the API request that
/// raised it never waits on fan-out work.
/// </summary>
public sealed record NotificationEvent(
    Guid EventId,
    string Type,
    int ActorId,
    int? RecipientId,
    int? PostId,
    string? Text,
    DateTime OccurredAt);

public static class NotificationTypes
{
    public const string PostCreated = "PostCreated";
    public const string PostLiked = "PostLiked";
    public const string PostUnliked = "PostUnliked";
    public const string PostCommented = "PostCommented";
    public const string FriendRequestSent = "FriendRequestSent";
    public const string FriendRequestAccepted = "FriendRequestAccepted";
    public const string UserFollowed = "UserFollowed";
}

public static class NotificationQueues
{
    public const string PostNotifications = "post-notifications";
    public const string FriendRequests = "friend-requests";

    public static string For(string type) => type is NotificationTypes.FriendRequestSent or NotificationTypes.FriendRequestAccepted or NotificationTypes.UserFollowed
        ? FriendRequests
        : PostNotifications;
}

public class NotificationDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string? Text { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public PostAuthorDto? Actor { get; set; }
    public int? PostId { get; set; }
    public string? PostImagePath { get; set; }
}
