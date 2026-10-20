namespace SocialMediaWeb.Models
{
    /// <summary>One-way Instagram-style follow, independent of mutual friendships.</summary>
    public class Follow
    {
        public int Id { get; set; }

        public int FollowerId { get; set; }
        public User? Follower { get; set; }

        public int FollowingId { get; set; }
        public User? Following { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
