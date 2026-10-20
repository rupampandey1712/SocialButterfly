using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class SocialMedia : ISocialMediaPost
    {
        private readonly ApplicationDbContext _context;
        private readonly IBlobStorageService _blobStorage;
        private readonly INotificationPublisher _notifications;

        private const string PostImagesContainer = "post-images";
        private const long MaximumMediaBytes = 80L * 1024 * 1024;

        public SocialMedia(
            ApplicationDbContext context,
            IBlobStorageService blobStorage,
            INotificationPublisher notifications)
        {
            _context = context;
            _blobStorage = blobStorage;
            _notifications = notifications;
        }

        public async Task<SocialMediaPost> CreatePostAsync(PostCreateDto createDto)
        {
            if (createDto.UserId == null || !await _context.Users.AnyAsync(u => u.Id == createDto.UserId && !u.IsDeleted))
            {
                throw new ArgumentException("A valid user is required to create a post.");
            }

            var post = new SocialMediaPost
            {
                Descriprion = createDto.Description,
                UserId = createDto.UserId,
                CreatedAt = createDto.CreatedAt ?? DateTime.UtcNow
            };

            var media = createDto.ImageFile is { Length: > 0 } ? createDto.ImageFile : null;
            var music = createDto.MusicFile is { Length: > 0 } ? createDto.MusicFile : null;

            if (string.IsNullOrWhiteSpace(createDto.Description) && media == null)
            {
                throw new ArgumentException("A post needs a caption or a photo, video or song.");
            }

            var mediaIsAudio = false;
            if (media != null)
            {
                mediaIsAudio = MediaRules.IsAudio(media);
                if (!MediaRules.IsImage(media) && !MediaRules.IsVideo(media) && !mediaIsAudio)
                {
                    throw new ArgumentException("Posts can be photos, videos or songs (MP3, M4A, WAV, OGG, FLAC).");
                }

                if (media.Length > MaximumMediaBytes)
                {
                    throw new ArgumentException("Media files must be 80 MB or smaller.");
                }
            }

            if (music != null)
            {
                if (media == null || mediaIsAudio)
                {
                    throw new ArgumentException("Music can only be added to a photo or video post.");
                }

                MediaRules.ValidateMusic(music);
            }

            if (media != null)
            {
                post.ImagePath = await _blobStorage.UploadAsync(media, mediaIsAudio ? MediaRules.MusicContainer : PostImagesContainer);
                if (mediaIsAudio)
                {
                    // A song post: the music metadata describes the uploaded track itself.
                    post.MusicTitle = MediaRules.CleanTitle(createDto.MusicTitle, media);
                    post.MusicArtist = MediaRules.CleanTitle(createDto.MusicArtist);
                }
            }

            if (music != null)
            {
                post.MusicPath = await _blobStorage.UploadAsync(music, MediaRules.MusicContainer);
                post.MusicTitle = MediaRules.CleanTitle(createDto.MusicTitle, music);
                post.MusicArtist = MediaRules.CleanTitle(createDto.MusicArtist);
            }

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Friends are notified asynchronously: Service Bus -> Functions -> Notifications table.
            await _notifications.PublishAsync(NotificationTypes.PostCreated, post.UserId!.Value, postId: post.Id);

            return post;
        }

        public async Task<List<SocialMediaPost>> AllPost()
        {
            return await _context.Posts.Include(p => p.User).ToListAsync();
        }

        public async Task<List<SocialMediaPost>> GetUserpost(int id)
        {
            return await _context.Posts.Include(p => p.User).Where(e => e.UserId == id).ToListAsync();
        }

        public async Task<List<PostFeedDto>> GetFeedAsync(int? viewerId, int skip, int take)
        {
            var posts = await ProjectToFeed(_context.Posts, viewerId)
                .Skip(Math.Max(skip, 0))
                .Take(Math.Clamp(take, 1, 50))
                .ToListAsync();

            return ResolveImageUrls(posts);
        }

        public async Task<List<PostFeedDto>> GetUserPostsAsync(int userId, int? viewerId)
        {
            var posts = await ProjectToFeed(_context.Posts.Where(p => p.UserId == userId), viewerId)
                .ToListAsync();

            return ResolveImageUrls(posts);
        }

        public async Task<PostFeedDto?> GetPostAsync(int postId, int? viewerId)
        {
            var post = await ProjectToFeed(_context.Posts.Where(p => p.Id == postId), viewerId)
                .FirstOrDefaultAsync();

            return post == null ? null : ResolveImageUrls(new List<PostFeedDto> { post })[0];
        }

        public async Task<bool> DeletePostAsync(int postId, int userId)
        {
            var post = await _context.Posts.FirstOrDefaultAsync(p => p.Id == postId);
            if (post == null)
            {
                return false;
            }

            if (post.UserId != userId)
            {
                throw new UnauthorizedAccessException("Only the author can delete this post.");
            }

            var imagePath = post.ImagePath;
            var musicPath = post.MusicPath;
            _context.Posts.Remove(post);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(imagePath) && Uri.TryCreate(imagePath, UriKind.Absolute, out var imageUri))
            {
                var container = imageUri.AbsolutePath.Contains($"/{MediaRules.MusicContainer}/") ? MediaRules.MusicContainer : PostImagesContainer;
                await _blobStorage.DeleteAsync(imagePath, container);
            }

            if (!string.IsNullOrWhiteSpace(musicPath))
            {
                await _blobStorage.DeleteAsync(musicPath, MediaRules.MusicContainer);
            }

            return true;
        }

        private static IQueryable<PostFeedDto> ProjectToFeed(IQueryable<SocialMediaPost> posts, int? viewerId)
        {
            return posts
                .AsNoTracking()
                .Where(p => p.User == null || !p.User.IsDeleted)
                .OrderByDescending(p => p.CreatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => new PostFeedDto
                {
                    Id = p.Id,
                    ImagePath = p.ImagePath,
                    Description = p.Descriprion,
                    CreatedAt = p.CreatedAt,
                    User = p.User == null ? null : new PostAuthorDto
                    {
                        Id = p.User.Id,
                        Name = p.User.Name,
                        ImagePath = p.User.ImagePath
                    },
                    MusicPath = p.MusicPath,
                    MusicTitle = p.MusicTitle,
                    MusicArtist = p.MusicArtist,
                    LikeCount = p.Likes.Count,
                    CommentCount = p.Comments.Count,
                    IsLiked = viewerId != null && p.Likes.Any(l => l.UserId == viewerId)
                });
        }

        private List<PostFeedDto> ResolveImageUrls(List<PostFeedDto> posts)
        {
            foreach (var post in posts)
            {
                post.ImagePath = _blobStorage.ToPublicUrl(post.ImagePath);
                post.MusicPath = _blobStorage.ToPublicUrl(post.MusicPath);
                if (post.User != null)
                {
                    post.User.ImagePath = _blobStorage.ToPublicUrl(post.User.ImagePath);
                }
            }

            return posts;
        }
    }
}
