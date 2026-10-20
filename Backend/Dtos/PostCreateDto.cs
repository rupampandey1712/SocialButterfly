namespace SocialMediaWeb.Dtos
{
    public class PostCreateDto
    {
        public IFormFile? ImageFile { get; set; }
        public string? Image { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? UserId { get; set; }

        /// <summary>Optional song to play with a photo/video post.</summary>
        public IFormFile? MusicFile { get; set; }
        public string? MusicTitle { get; set; }
        public string? MusicArtist { get; set; }
    }
}

