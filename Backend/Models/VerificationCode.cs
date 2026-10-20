using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Models
{
    /// <summary>
    /// One-time code for verifying an email/phone, passwordless login or password reset.
    /// Only a SHA-256 hash of the code is stored.
    /// </summary>
    public class VerificationCode
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        /// <summary>Normalized email address or E.164 phone number the code was sent to.</summary>
        [Required]
        [MaxLength(256)]
        public string Target { get; set; } = string.Empty;

        /// <summary>"Email" or "Sms".</summary>
        [Required]
        [MaxLength(10)]
        public string Channel { get; set; } = string.Empty;

        /// <summary>"Verify", "Login" or "Reset".</summary>
        [Required]
        [MaxLength(10)]
        public string Purpose { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string CodeHash { get; set; } = string.Empty;

        public int Attempts { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime ExpiresAt { get; set; }

        public DateTime? ConsumedAt { get; set; }
    }
}
