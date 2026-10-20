using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Models
{
    /// <summary>A 24-hour story. Media lives in the "stories" blob container.</summary>
    public class Story
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        [Required]
        [MaxLength(1000)]
        public string MediaPath { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Caption { get; set; }

        /// <summary>Optional background music (blob URL in the "music" container).</summary>
        [MaxLength(1000)]
        public string? MusicPath { get; set; }

        [MaxLength(120)]
        public string? MusicTitle { get; set; }

        [MaxLength(120)]
        public string? MusicArtist { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

        public ICollection<StoryView> Views { get; set; } = new List<StoryView>();
    }

    public class StoryView
    {
        public int Id { get; set; }

        public int StoryId { get; set; }
        public Story? Story { get; set; }

        public int ViewerId { get; set; }
        public User? Viewer { get; set; }

        public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    }
}
