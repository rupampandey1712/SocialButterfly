namespace SocialMediaWeb.Dtos
{
    public class UserScheduleDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? Imagepath { get; set; }
        public bool IsDeleted { get; set; }
    }
}
