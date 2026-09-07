using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SocialMediaWeb.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        /// <summary>
        /// Stable id supplied to the queue consumer. It makes processing safe when
        /// Service Bus redelivers a message after a transient failure.
        /// </summary>
        public Guid MessageId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public DateTime? DeliveredAt { get; set; }

        public bool IsRead { get; set; } = false;

        [ForeignKey("Sender")]
        public int SenderId { get; set; }
        public User? Sender { get; set; }

        [ForeignKey("Receiver")]
        public int ReceiverId { get; set; }
        public User? Receiver { get; set; }
    }
}
