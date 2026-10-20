namespace SocialMediaWeb.Dtos
{
    public class PostAuthorDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImagePath { get; set; }
    }

    /// <summary>
    /// Feed-ready view of a post: author, counts and whether the viewer liked it,
    /// so the client can render an Instagram-style card without extra calls.
    /// </summary>
    public class PostFeedDto
    {
        public int Id { get; set; }
        public string? ImagePath { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public PostAuthorDto? User { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public bool IsLiked { get; set; }
        public string? MusicPath { get; set; }
        public string? MusicTitle { get; set; }
        public string? MusicArtist { get; set; }
    }

    public class LikeToggleResultDto
    {
        public int PostId { get; set; }
        public int LikeCount { get; set; }
        public bool IsLiked { get; set; }
    }

    public class CommentResponseDto
    {
        public int Id { get; set; }
        public int PostId { get; set; }
        public string? Text { get; set; }
        public DateTime CreatedAt { get; set; }
        public PostAuthorDto? User { get; set; }
    }

    public class UserProfileDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Username { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowing { get; set; }
        public bool FollowsYou { get; set; }
        public bool HasActiveStory { get; set; }
        public string? ImagePath { get; set; }
        public string? Information { get; set; }
        public int PostsCount { get; set; }
        public int FriendsCount { get; set; }
        public string FriendshipStatus { get; set; } = "None";
    }
}
