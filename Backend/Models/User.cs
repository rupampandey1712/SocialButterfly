using System.ComponentModel.DataAnnotations;

namespace SocialMediaWeb.Models
{
    public class User
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        public DateTime Dob { get; set; }

        [DataType(DataType.EmailAddress)]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        public string? PasswordHash { get; set; }

        public bool IsDeleted { get; set; } = false;

        public string? Role { get; set; }

        public string? ImagePath { get; set; }

        public string? Information { get;set; }

        /// <summary>Unique handle (lowercase letters, digits, '.' and '_'). Can be used to log in.</summary>
        [MaxLength(30)]
        public string? Username { get; set; }

        /// <summary>E.164 phone number (e.g. +919876543210). Can be used to log in.</summary>
        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        public bool EmailConfirmed { get; set; }

        public bool PhoneConfirmed { get; set; }

        /// <summary>"light", "dark" or "system".</summary>
        [MaxLength(10)]
        public string Theme { get; set; } = "system";

        // Notification preferences are stored as opt-outs so every existing user keeps them on.
        public bool MuteLikeNotifications { get; set; }
        public bool MuteCommentNotifications { get; set; }
        public bool MuteFollowNotifications { get; set; }
        public bool MuteNewPostNotifications { get; set; }

        public virtual ICollection<Friend> Senders { get; set; } = new List<Friend>();

        public virtual ICollection<Friend> Receivers { get; set; } = new List<Friend>();



    }
}
