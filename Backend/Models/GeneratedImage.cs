using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Models
{
    /// <summary>
    /// An AI image request. Created by the API, processed asynchronously by the Functions
    /// worker (Service Bus "image-generation" -> ComfyUI), stored in the "ai-images" container.
    /// </summary>
    public class GeneratedImage
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>What the member typed.</summary>
        [Required]
        [MaxLength(800)]
        public string Prompt { get; set; } = string.Empty;

        [MaxLength(30)]
        public string Style { get; set; } = "none";

        public int Width { get; set; }
        public int Height { get; set; }
        public long Seed { get; set; }

        /// <summary>Queued, Running, Succeeded or Failed.</summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = GeneratedImageStatus.Queued;

        [MaxLength(1000)]
        public string? ImagePath { get; set; }

        [MaxLength(300)]
        public string? Error { get; set; }

        public int? PostId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public static class GeneratedImageStatus
    {
        public const string Queued = "Queued";
        public const string Running = "Running";
        public const string Succeeded = "Succeeded";
        public const string Failed = "Failed";
    }
}
