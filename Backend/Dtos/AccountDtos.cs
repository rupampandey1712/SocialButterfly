namespace SocialMediaWeb.Dtos
{
    public static class VerificationPurposes
    {
        public const string Verify = "Verify";
        public const string Login = "Login";
        public const string Reset = "Reset";

        public static bool IsValid(string? purpose) => purpose is Verify or Login or Reset;
    }

    public static class VerificationChannels
    {
        public const string Email = "Email";
        public const string Sms = "Sms";
    }

    public class SendCodeRequestDto
    {
        /// <summary>Email address or phone number.</summary>
        public string? Target { get; set; }

        /// <summary>"Verify", "Login" or "Reset".</summary>
        public string? Purpose { get; set; }

        /// <summary>Honeypot: hidden from people, so only bots fill it in.</summary>
        public string? Website { get; set; }
    }

    public class SendCodeResponseDto
    {
        public string Channel { get; set; } = string.Empty;

        /// <summary>Masked destination, e.g. "a***@gmail.com" or "+91******3210".</summary>
        public string Destination { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Only populated in Development so the flow can be tested without a real
        /// email/SMS provider. Never returned in other environments.
        /// </summary>
        public string? DevCode { get; set; }
    }

    public class ConfirmCodeRequestDto
    {
        public string? Target { get; set; }
        public string? Purpose { get; set; }
        public string? Code { get; set; }

        /// <summary>Required when Purpose is "Reset".</summary>
        public string? NewPassword { get; set; }

        /// <summary>Honeypot: hidden from people, so only bots fill it in.</summary>
        public string? Website { get; set; }
    }

    public class RegisterResponseDto
    {
        public int Id { get; set; }
        public string? Username { get; set; }

        /// <summary>The email or phone that still needs to be verified.</summary>
        public string? VerificationTarget { get; set; }
    }

    public class AvailabilityDto
    {
        public bool? UsernameAvailable { get; set; }
        public bool? EmailAvailable { get; set; }
        public bool? PhoneAvailable { get; set; }
        public string? SuggestedUsername { get; set; }
    }

    public class UserSettingsDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneConfirmed { get; set; }
        public string? Information { get; set; }
        public string? ImagePath { get; set; }
        public DateTime Dob { get; set; }
        public string Theme { get; set; } = "system";
        public bool NotifyLikes { get; set; } = true;
        public bool NotifyComments { get; set; } = true;
        public bool NotifyFollows { get; set; } = true;
        public bool NotifyNewPosts { get; set; } = true;
    }

    public class UpdateSettingsDto
    {
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Information { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? Theme { get; set; }
        public bool? NotifyLikes { get; set; }
        public bool? NotifyComments { get; set; }
        public bool? NotifyFollows { get; set; }
        public bool? NotifyNewPosts { get; set; }
    }

    public class ChangePasswordDto
    {
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }

    /// <summary>A row in followers / following / people lists.</summary>
    public class UserListItemDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? ImagePath { get; set; }
        public string? Information { get; set; }
        public bool IsFollowing { get; set; }
        public bool FollowsYou { get; set; }
        public bool IsSelf { get; set; }
        public int FollowersCount { get; set; }
    }

    public class FollowResultDto
    {
        public bool IsFollowing { get; set; }
        public int FollowersCount { get; set; }
    }

    public class StoryItemDto
    {
        public int Id { get; set; }
        public string MediaPath { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Seen { get; set; }
        public int ViewCount { get; set; }
        public string? MusicPath { get; set; }
        public string? MusicTitle { get; set; }
        public string? MusicArtist { get; set; }
    }

    public class StoryGroupDto
    {
        public PostAuthorDto User { get; set; } = new();
        public string? Username { get; set; }
        public bool IsOwn { get; set; }
        public bool HasUnseen { get; set; }
        public DateTime LatestAt { get; set; }
        public List<StoryItemDto> Stories { get; set; } = new();
    }

    public class StoryViewerDto
    {
        public PostAuthorDto User { get; set; } = new();
        public DateTime ViewedAt { get; set; }
    }

    public sealed record VerificationCodeMessage(
        Guid EventId,
        string Channel,
        string Target,
        string Purpose,
        string Code,
        DateTime ExpiresAt);
}
