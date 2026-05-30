using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class SocialMedia : ISocialMediaPost
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SocialMedia(
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _httpContextAccessor = httpContextAccessor;
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
                var request = _httpContextAccessor.HttpContext?.Request
                    ?? throw new InvalidOperationException("The current HTTP request is not available.");
                var baseUrl = $"{request.Scheme}://{request.Host}";
                var fileName = Path.GetFileNameWithoutExtension(createDto.ImageFile.FileName);
                var fileExtension = Path.GetExtension(createDto.ImageFile.FileName);
                var uniqueFileName = $"{fileName}_{DateTime.UtcNow.Ticks}{fileExtension}";
                var webRootPath = _webHostEnvironment.WebRootPath
                    ?? Path.Combine(_webHostEnvironment.ContentRootPath, "wwwroot");
                var uploadPath = Path.Combine(webRootPath, "posts");
                Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, uniqueFileName);

                await using var fileStream = new FileStream(filePath, FileMode.Create);
                await createDto.ImageFile.CopyToAsync(fileStream);

                post.ImagePath = $"{baseUrl}/posts/{uniqueFileName}";
            }

            _context.Posts.Add(post);
            await _context.SaveChangesAsync();

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
