using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// AI image studio (API side): validates and queues jobs, enforces per-member quotas,
    /// and shares finished images as posts. The GPU work happens in the Functions worker.
    /// </summary>
    public partial class ImageGenerationService : IImageGenerationService
    {
        public const string QueueName = "image-generation";
        public const string Container = "ai-images";
        public const int MaxActiveJobsPerUser = 2;
        public const int MaxJobsPerDay = 25;
        public const int MaxPromptLength = 800;

        /// <summary>Style presets are appended server-side, so clients can't inject arbitrary templates.</summary>
        public static readonly IReadOnlyDictionary<string, string> Styles = new Dictionary<string, string>
        {
            ["none"] = "",
            ["photo"] = ", professional photograph, natural light, 35mm, highly detailed",
            ["cinematic"] = ", cinematic film still, dramatic lighting, shallow depth of field, color graded",
            ["anime"] = ", anime illustration, vibrant colors, clean line art, studio quality",
            ["watercolor"] = ", delicate watercolor painting, soft washes, textured paper",
            ["3d"] = ", 3D render, soft studio lighting, octane render, highly detailed",
            ["neon"] = ", neon cyberpunk aesthetic, glowing lights, night, rain reflections",
            ["sketch"] = ", pencil sketch, detailed graphite shading, on paper",
        };

        private static readonly Dictionary<string, (int Width, int Height)> Aspects = new()
        {
            ["square"] = (1024, 1024),
            ["portrait"] = (768, 1152),
            ["landscape"] = (1152, 768),
        };

        // A minimal safety screen. Production should use a real moderation service
        // (e.g. Azure AI Content Safety) on both the prompt and the generated image.
        [GeneratedRegex(@"\b(child|children|kid|kids|minor|minors|underage|teen|preteen|loli|toddler)\b[\s\S]*\b(nude|naked|sexual|sexy|nsfw|explicit|porn)\b|\b(nude|naked|sexual|sexy|nsfw|explicit|porn)\b[\s\S]*\b(child|children|kid|kids|minor|minors|underage|teen|preteen|loli|toddler)\b",
            RegexOptions.IgnoreCase)]
        private static partial Regex ProhibitedContent();

        private readonly ApplicationDbContext _context;
        private readonly IServiceBusService _serviceBus;
        private readonly IBlobStorageService _blobStorage;
        private readonly INotificationPublisher? _notifications;

        public ImageGenerationService(
            ApplicationDbContext context,
            IServiceBusService serviceBus,
            IBlobStorageService blobStorage,
            INotificationPublisher? notifications = null)
        {
            _context = context;
            _serviceBus = serviceBus;
            _blobStorage = blobStorage;
            _notifications = notifications;
        }

        public async Task<GeneratedImageDto> CreateAsync(int userId, GenerateImageRequestDto request)
        {
            var prompt = CleanPrompt(request.Prompt);
            if (prompt.Length < 3)
            {
                throw new ArgumentException("Describe the image you want (at least a few words).");
            }

            if (ProhibitedContent().IsMatch(prompt))
            {
                throw new ArgumentException("This prompt isn't allowed.");
            }

            var style = (request.Style ?? "none").ToLowerInvariant();
            if (!Styles.ContainsKey(style))
            {
                throw new ArgumentException("Unknown style.");
            }

            if (!Aspects.TryGetValue((request.Aspect ?? "square").ToLowerInvariant(), out var size))
            {
                throw new ArgumentException("Aspect must be square, portrait or landscape.");
            }

            var active = await _context.GeneratedImages.CountAsync(g => g.UserId == userId &&
                (g.Status == GeneratedImageStatus.Queued || g.Status == GeneratedImageStatus.Running));
            if (active >= MaxActiveJobsPerUser)
            {
                throw new InvalidOperationException("You already have images being created. Wait for them to finish.");
            }

            if (await UsedTodayAsync(userId) >= MaxJobsPerDay)
            {
                throw new InvalidOperationException($"You've reached today's limit of {MaxJobsPerDay} images. Try again tomorrow.");
            }

            var job = new GeneratedImage
            {
                UserId = userId,
                Prompt = prompt,
                Style = style,
                Width = size.Width,
                Height = size.Height,
                Seed = request.Seed is > 0 and < long.MaxValue ? request.Seed.Value : RandomNumberGenerator.GetInt32(1, int.MaxValue),
                Status = GeneratedImageStatus.Queued
            };
            _context.GeneratedImages.Add(job);
            await _context.SaveChangesAsync();

            try
            {
                await _serviceBus.SendMessageAsync(QueueName, new ImageGenerationMessage(job.Id), $"image-{job.Id}");
            }
            catch (Exception)
            {
                job.Status = GeneratedImageStatus.Failed;
                job.Error = "The image service is busy right now. Please try again.";
                job.CompletedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return ToDto(job);
        }

        public async Task<GeneratedImageDto?> GetAsync(int id, int userId)
        {
            var job = await _context.GeneratedImages.AsNoTracking().FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);
            return job == null ? null : ToDto(job);
        }

        public async Task<List<GeneratedImageDto>> ListMineAsync(int userId, int take)
        {
            var jobs = await _context.GeneratedImages.AsNoTracking()
                .Where(g => g.UserId == userId)
                .OrderByDescending(g => g.CreatedAt)
                .Take(Math.Clamp(take, 1, 60))
                .ToListAsync();
            return jobs.Select(ToDto).ToList();
        }

        public async Task<int> CountActiveAsync(int userId) =>
            await _context.GeneratedImages.CountAsync(g => g.UserId == userId &&
                (g.Status == GeneratedImageStatus.Queued || g.Status == GeneratedImageStatus.Running));

        public async Task<int> UsedTodayAsync(int userId)
        {
            var since = DateTime.UtcNow.AddDays(-1);
            return await _context.GeneratedImages.CountAsync(g => g.UserId == userId && g.CreatedAt > since);
        }

        public async Task<int> ShareAsPostAsync(int id, int userId, string? caption)
        {
            var job = await _context.GeneratedImages.FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId)
                ?? throw new KeyNotFoundException("Image not found.");

            if (job.Status != GeneratedImageStatus.Succeeded || job.ImagePath == null)
            {
                throw new InvalidOperationException("This image isn't ready yet.");
            }

            if (job.PostId is int existing && await _context.Posts.AnyAsync(p => p.Id == existing))
            {
                return existing;
            }

            var text = string.IsNullOrWhiteSpace(caption) ? job.Prompt : caption.Trim();
            var post = new SocialMediaPost
            {
                UserId = userId,
                Descriprion = text.Length > 2200 ? text[..2200] : text,
                ImagePath = job.ImagePath,
                CreatedAt = DateTime.UtcNow
            };
            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            job.PostId = post.Id;
            await _context.SaveChangesAsync();

            if (_notifications != null)
            {
                await _notifications.PublishAsync(NotificationTypes.PostCreated, userId, postId: post.Id);
            }

            return post.Id;
        }

        /// <summary>The prompt actually sent to the model: member text + server-side style preset.</summary>
        public static string FullPrompt(GeneratedImage job) =>
            job.Prompt + (Styles.TryGetValue(job.Style, out var suffix) ? suffix : string.Empty);

        public static string CleanPrompt(string? prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;
            var cleaned = new string(prompt.Where(c => !char.IsControl(c) || c == '\n').ToArray()).Trim();
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            return cleaned.Length > MaxPromptLength ? cleaned[..MaxPromptLength] : cleaned;
        }

        private GeneratedImageDto ToDto(GeneratedImage job) => new()
        {
            Id = job.Id,
            Prompt = job.Prompt,
            Style = job.Style,
            Width = job.Width,
            Height = job.Height,
            Seed = job.Seed,
            Status = job.Status,
            ImagePath = _blobStorage.ToPublicUrl(job.ImagePath),
            Error = job.Error,
            PostId = job.PostId,
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt
        };
    }
}
