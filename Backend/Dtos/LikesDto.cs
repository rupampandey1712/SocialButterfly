namespace SocialMediaWeb.Dtos
{
    public class LikesDto
    {
        public int Id { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        public int UserId { get; set; }

        public int PostId { get; set; }
    }
}
