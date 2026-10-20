namespace SocialMediaWeb.Dtos
{
    public class GenerateImageRequestDto
    {
        public string? Prompt { get; set; }

        /// <summary>"square", "portrait" or "landscape".</summary>
        public string? Aspect { get; set; }

        /// <summary>One of the preset styles (see ImageGenerationService.Styles), or "none".</summary>
        public string? Style { get; set; }

        /// <summary>Optional, to reproduce an image; random when omitted.</summary>
        public long? Seed { get; set; }
    }

    public class GeneratedImageDto
    {
        public int Id { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string Style { get; set; } = "none";
        public int Width { get; set; }
        public int Height { get; set; }
        public long Seed { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
        public string? Error { get; set; }
        public int? PostId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ShareGeneratedImageDto
    {
        public string? Caption { get; set; }
    }

    public class ImageStudioStatusDto
    {
        public bool Online { get; set; }
        public int ActiveJobs { get; set; }
        public int RemainingToday { get; set; }
        public string[] Styles { get; set; } = Array.Empty<string>();
    }

    public sealed record ImageGenerationMessage(int JobId);
}
