namespace SocialMediaWeb.Services.Classes
{
    /// <summary>What can be uploaded where. Content types come from the client, so extensions are checked too.</summary>
    public static class MediaRules
    {
        public const string MusicContainer = "music";
        public const long MaximumMusicBytes = 25L * 1024 * 1024;

        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif" };
        private static readonly string[] VideoExtensions = { ".mp4", ".webm", ".mov", ".m4v", ".ogv" };
        private static readonly string[] AudioExtensions = { ".mp3", ".m4a", ".aac", ".wav", ".ogg", ".oga", ".opus", ".flac", ".weba" };

        public static bool IsImage(IFormFile file) => Matches(file, "image/", ImageExtensions);
        public static bool IsVideo(IFormFile file) => Matches(file, "video/", VideoExtensions);
        public static bool IsAudio(IFormFile file) => Matches(file, "audio/", AudioExtensions);

        /// <summary>Validates an optional soundtrack and returns a trimmed title (falls back to the file name).</summary>
        public static void ValidateMusic(IFormFile music)
        {
            if (!IsAudio(music))
            {
                throw new ArgumentException("Music must be an audio file (MP3, M4A, AAC, WAV, OGG or FLAC).");
            }

            if (music.Length > MaximumMusicBytes)
            {
                throw new ArgumentException("Music files must be 25 MB or smaller.");
            }
        }

        public static string? CleanTitle(string? value, IFormFile? fallbackFile = null)
        {
            var title = string.IsNullOrWhiteSpace(value)
                ? fallbackFile == null ? null : Path.GetFileNameWithoutExtension(fallbackFile.FileName).Replace('_', ' ')
                : value.Trim();
            return title is { Length: > 120 } ? title[..120] : title;
        }

        private static bool Matches(IFormFile file, string mimePrefix, string[] extensions)
        {
            var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            return (file.ContentType ?? string.Empty).StartsWith(mimePrefix, StringComparison.OrdinalIgnoreCase)
                   && extensions.Contains(extension);
        }
    }
}
