using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// 24-hour stories. Media is stored in the "stories" blob container; a Functions
    /// timer (CleanupExpiredStories) deletes expired stories and their blobs.
    /// </summary>
    public class StoryService : IStoryService
    {
        public const string StoriesContainer = "stories";
        public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
        private const long MaximumMediaBytes = 50L * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly IBlobStorageService _blobStorage;
        private readonly ILogger<StoryService>? _logger;

        public StoryService(ApplicationDbContext context, IBlobStorageService blobStorage, ILogger<StoryService>? logger = null)
        {
            _context = context;
            _blobStorage = blobStorage;
            _logger = logger;
        }

        public async Task<StoryItemDto> CreateAsync(int userId, IFormFile? media, string? caption,
            IFormFile? music = null, string? musicTitle = null, string? musicArtist = null)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted))
            {
                throw new KeyNotFoundException("User cannot be found");
            }

            if (media == null || media.Length == 0)
            {
                throw new ArgumentException("Choose a photo or video for your story.");
            }

            if (!MediaRules.IsImage(media) && !MediaRules.IsVideo(media))
            {
                throw new ArgumentException("Stories can only be photos or videos.");
            }

            if (music is { Length: > 0 })
            {
                MediaRules.ValidateMusic(music);
            }

            if (media.Length > MaximumMediaBytes)
            {
                throw new ArgumentException("Story media must be 50 MB or smaller.");
            }

            var now = DateTime.UtcNow;
            var story = new Story
            {
                UserId = userId,
                MediaPath = await _blobStorage.UploadAsync(media, StoriesContainer),
                Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim()[..Math.Min(caption.Trim().Length, 200)],
                CreatedAt = now,
                ExpiresAt = now.Add(Lifetime)
            };
            if (music is { Length: > 0 })
            {
                story.MusicPath = await _blobStorage.UploadAsync(music, MediaRules.MusicContainer);
                story.MusicTitle = MediaRules.CleanTitle(musicTitle, music);
                story.MusicArtist = MediaRules.CleanTitle(musicArtist);
            }
            _context.Stories.Add(story);
            await _context.SaveChangesAsync();

            return new StoryItemDto
            {
                Id = story.Id,
                MediaPath = _blobStorage.ToPublicUrl(story.MediaPath) ?? story.MediaPath,
                Caption = story.Caption,
                CreatedAt = story.CreatedAt,
                ExpiresAt = story.ExpiresAt,
                Seen = true,
                MusicPath = _blobStorage.ToPublicUrl(story.MusicPath),
                MusicTitle = story.MusicTitle,
                MusicArtist = story.MusicArtist
            };
        }

        public async Task<List<StoryGroupDto>> GetFeedAsync(int viewerId)
        {
            var now = DateTime.UtcNow;

            // Your own stories, plus people you follow and your friends.
            var authorIds = await _context.Follows
                .Where(f => f.FollowerId == viewerId)
                .Select(f => f.FollowingId)
                .Union(_context.Friends
                    .Where(f => f.IsAccepted && (f.SenderId == viewerId || f.ReceiverId == viewerId))
                    .Select(f => f.SenderId == viewerId ? f.ReceiverId!.Value : f.SenderId!.Value))
                .ToListAsync();
            authorIds.Add(viewerId);

            var stories = await _context.Stories
                .AsNoTracking()
                .Where(s => authorIds.Contains(s.UserId) && s.ExpiresAt > now && s.User != null && !s.User.IsDeleted)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new
                {
                    s.Id,
                    s.UserId,
                    s.MediaPath,
                    s.Caption,
                    s.CreatedAt,
                    s.ExpiresAt,
                    s.MusicPath,
                    s.MusicTitle,
                    s.MusicArtist,
                    UserName = s.User!.Name,
                    s.User.Username,
                    UserImage = s.User.ImagePath,
                    Seen = s.Views.Any(v => v.ViewerId == viewerId),
                    ViewCount = s.Views.Count
                })
                .ToListAsync();

            var groups = stories
                .GroupBy(s => s.UserId)
                .Select(g =>
                {
                    var first = g.First();
                    var isOwn = g.Key == viewerId;
                    var items = g.Select(s => new StoryItemDto
                    {
                        Id = s.Id,
                        MediaPath = _blobStorage.ToPublicUrl(s.MediaPath) ?? s.MediaPath,
                        Caption = s.Caption,
                        CreatedAt = s.CreatedAt,
                        ExpiresAt = s.ExpiresAt,
                        Seen = isOwn || s.Seen,
                        ViewCount = isOwn ? s.ViewCount : 0,
                        MusicPath = _blobStorage.ToPublicUrl(s.MusicPath),
                        MusicTitle = s.MusicTitle,
                        MusicArtist = s.MusicArtist
                    }).ToList();

                    return new StoryGroupDto
                    {
                        User = new PostAuthorDto
                        {
                            Id = g.Key,
                            Name = first.UserName,
                            ImagePath = _blobStorage.ToPublicUrl(first.UserImage)
                        },
                        Username = first.Username,
                        IsOwn = isOwn,
                        HasUnseen = items.Any(i => !i.Seen),
                        LatestAt = items.Max(i => i.CreatedAt),
                        Stories = items
                    };
                })
                .OrderByDescending(g => g.IsOwn)
                .ThenByDescending(g => g.HasUnseen)
                .ThenByDescending(g => g.LatestAt)
                .ToList();

            return groups;
        }

        public async Task<List<StoryGroupDto>> GetUserStoriesAsync(int userId, int viewerId)
        {
            var feed = await GetFeedAsync(viewerId);
            var group = feed.FirstOrDefault(g => g.User.Id == userId);
            if (group != null)
            {
                return new List<StoryGroupDto> { group };
            }

            // Public profiles: anyone can open an active story from the profile avatar.
            var now = DateTime.UtcNow;
            var hasStories = await _context.Stories.AnyAsync(s => s.UserId == userId && s.ExpiresAt > now);
            if (!hasStories)
            {
                return new List<StoryGroupDto>();
            }

            var user = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == userId);
            var items = await _context.Stories.AsNoTracking()
                .Where(s => s.UserId == userId && s.ExpiresAt > now)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new StoryItemDto
                {
                    Id = s.Id,
                    MediaPath = s.MediaPath,
                    Caption = s.Caption,
                    CreatedAt = s.CreatedAt,
                    ExpiresAt = s.ExpiresAt,
                    Seen = s.Views.Any(v => v.ViewerId == viewerId),
                    MusicPath = s.MusicPath,
                    MusicTitle = s.MusicTitle,
                    MusicArtist = s.MusicArtist
                })
                .ToListAsync();
            items.ForEach(i =>
            {
                i.MediaPath = _blobStorage.ToPublicUrl(i.MediaPath) ?? i.MediaPath;
                i.MusicPath = _blobStorage.ToPublicUrl(i.MusicPath);
            });

            return new List<StoryGroupDto>
            {
                new()
                {
                    User = new PostAuthorDto { Id = user.Id, Name = user.Name, ImagePath = _blobStorage.ToPublicUrl(user.ImagePath) },
                    Username = user.Username,
                    HasUnseen = items.Any(i => !i.Seen),
                    LatestAt = items.Max(i => i.CreatedAt),
                    Stories = items
                }
            };
        }

        public async Task MarkViewedAsync(int storyId, int viewerId)
        {
            var story = await _context.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storyId);
            if (story == null || story.UserId == viewerId)
            {
                return;
            }

            if (await _context.StoryViews.AnyAsync(v => v.StoryId == storyId && v.ViewerId == viewerId))
            {
                return;
            }

            _context.StoryViews.Add(new StoryView { StoryId = storyId, ViewerId = viewerId });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Two quick views raced on the unique (story, viewer) index; one row is enough.
                _context.ChangeTracker.Clear();
            }
        }

        public async Task<List<StoryViewerDto>> GetViewersAsync(int storyId, int ownerId)
        {
            var story = await _context.Stories.AsNoTracking().FirstOrDefaultAsync(s => s.Id == storyId)
                ?? throw new KeyNotFoundException("Story not found");
            if (story.UserId != ownerId)
            {
                throw new UnauthorizedAccessException("Only the author can see who viewed a story.");
            }

            var viewers = await _context.StoryViews.AsNoTracking()
                .Where(v => v.StoryId == storyId)
                .OrderByDescending(v => v.ViewedAt)
                .Select(v => new StoryViewerDto
                {
                    ViewedAt = v.ViewedAt,
                    User = new PostAuthorDto { Id = v.ViewerId, Name = v.Viewer!.Name, ImagePath = v.Viewer.ImagePath }
                })
                .ToListAsync();
            viewers.ForEach(v => v.User.ImagePath = _blobStorage.ToPublicUrl(v.User.ImagePath));
            return viewers;
        }

        public async Task<bool> DeleteAsync(int storyId, int userId)
        {
            var story = await _context.Stories.FirstOrDefaultAsync(s => s.Id == storyId);
            if (story == null)
            {
                return false;
            }

            if (story.UserId != userId)
            {
                throw new UnauthorizedAccessException("Only the author can delete this story.");
            }

            _context.Stories.Remove(story);
            await _context.SaveChangesAsync();
            await DeleteBlobsAsync(story);
            return true;
        }

        private async Task DeleteBlobsAsync(Story story)
        {
            await _blobStorage.DeleteAsync(story.MediaPath, StoriesContainer);
            if (!string.IsNullOrWhiteSpace(story.MusicPath))
            {
                await _blobStorage.DeleteAsync(story.MusicPath, MediaRules.MusicContainer);
            }
        }

        public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expired = await _context.Stories
                .Where(s => s.ExpiresAt <= now)
                .Take(500)
                .ToListAsync(cancellationToken);

            var removed = 0;
            foreach (var story in expired)
            {
                try
                {
                    await DeleteBlobsAsync(story);
                }
                catch (Exception ex)
                {
                    // Leave the row so the next run retries the blob delete.
                    _logger?.LogWarning(ex, "Could not delete blob for expired story {StoryId}", story.Id);
                    continue;
                }

                _context.Stories.Remove(story);
                removed++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return removed;
        }
    }
}
