using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Dtos
{
    public class UserLoginDto
    {

        public string? Email { get; set; }

        /// <summary>Email, phone number or username. Falls back to <see cref="Email"/>.</summary>
        public string? Identifier { get; set; }

        [DataType(DataType.Password)]
        public string? Password { get; set; }

        /// <summary>Honeypot: hidden from people, so only bots fill it in.</summary>
        public string? Website { get; set; }
    }
}
