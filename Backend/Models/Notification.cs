using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Models
{
    public class Notification
    {
        public int Id { get; set; }

        /// <summary>
        /// Id of the Service Bus event that produced this row. Unique per recipient,
        /// which makes queue redeliveries idempotent.
        /// </summary>
        public Guid EventId { get; set; }

        [Required]
        [MaxLength(40)]
        public string Type { get; set; } = string.Empty;

        public int RecipientId { get; set; }
        public User? Recipient { get; set; }

        public int ActorId { get; set; }
        public User? Actor { get; set; }

        public int? PostId { get; set; }
        public SocialMediaPost? Post { get; set; }

        [MaxLength(300)]
        public string? Text { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; }
    }
}
