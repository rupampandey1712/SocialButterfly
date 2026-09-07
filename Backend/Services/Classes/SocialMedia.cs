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
        private readonly IServiceBusService _serviceBus;

        private const string PostImagesContainer = "post-images";
        private const string PostNotificationsQueue = "post-notifications";

        public SocialMedia(
            ApplicationDbContext context,
            IBlobStorageService blobStorage,
            IServiceBusService serviceBus)
        {
            _context = context;
            _blobStorage = blobStorage;
            _serviceBus = serviceBus;
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

            if (createDto.ImageFile != null && createDto.ImageFile.Length > 0)
            {
                post.ImagePath = await _blobStorage.UploadAsync(createDto.ImageFile, PostImagesContainer);
            }

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

            // Send async notification via Service Bus
            await _serviceBus.SendMessageAsync(PostNotificationsQueue, new
            {
                PostId = post.Id,
                UserId = post.UserId,
                CreatedAt = post.CreatedAt,
                Event = "PostCreated"
            });

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
    }
}
