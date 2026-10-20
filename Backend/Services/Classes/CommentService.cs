using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class CommentService : ICommentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IBlobStorageService? _blobStorage;
        private readonly INotificationPublisher? _notifications;

        public CommentService(
            ApplicationDbContext dbContext,
            IBlobStorageService? blobStorage = null,
            INotificationPublisher? notifications = null)
        {
            _dbContext = dbContext;
            _blobStorage = blobStorage;
            _notifications = notifications;
        }

        public async Task<Comment> CreateCommentAsync(CreateCommentDto commentCreateDto)
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.Id == commentCreateDto.UserId && !u.IsDeleted);
            var postExists = await _dbContext.Posts.AnyAsync(p => p.Id == commentCreateDto.PostId);
            if (!userExists || !postExists)
            {
                throw new ArgumentException("A valid user and post are required to create a comment.");
            }

            var comment = new Comment
            {
                Text = commentCreateDto.Text,
                UserId = commentCreateDto.UserId,
                PostId = commentCreateDto.PostId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Comments.Add(comment);
            await _dbContext.SaveChangesAsync();

            if (_notifications != null)
            {
                await _notifications.PublishAsync(NotificationTypes.PostCommented, comment.UserId, postId: comment.PostId, text: comment.Text);
            }

            return comment;
        }

        public async Task<List<Comment>> GetCommentAsync(int postId)
        {
            return await _dbContext.Comments
                .Include(c => c.User)
                .Where(c => c.PostId == postId)
                .ToListAsync();
        }

        public async Task<List<CommentResponseDto>> GetPostCommentsAsync(int postId)
        {
            var comments = await _dbContext.Comments
                .AsNoTracking()
                .Where(c => c.PostId == postId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new CommentResponseDto
                {
                    Id = c.Id,
                    PostId = c.PostId,
                    Text = c.Text,
                    CreatedAt = c.CreatedAt,
                    User = c.User == null ? null : new PostAuthorDto
                    {
                        Id = c.User.Id,
                        Name = c.User.Name,
                        ImagePath = c.User.ImagePath
                    }
                })
                .ToListAsync();

            foreach (var comment in comments)
            {
                ResolveAuthorImage(comment);
            }

            return comments;
        }

        public async Task<CommentResponseDto> AddCommentAsync(CreateCommentDto commentCreateDto)
        {
            if (string.IsNullOrWhiteSpace(commentCreateDto.Text))
            {
                throw new ArgumentException("Comment text is required.");
            }

            var comment = await CreateCommentAsync(commentCreateDto);
            var author = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == comment.UserId)
                .Select(u => new PostAuthorDto { Id = u.Id, Name = u.Name, ImagePath = u.ImagePath })
                .FirstOrDefaultAsync();

            var response = new CommentResponseDto
            {
                Id = comment.Id,
                PostId = comment.PostId,
                Text = comment.Text,
                CreatedAt = comment.CreatedAt,
                User = author
            };
            ResolveAuthorImage(response);
            return response;
        }

        private void ResolveAuthorImage(CommentResponseDto comment)
        {
            if (_blobStorage != null && comment.User != null)
            {
                comment.User.ImagePath = _blobStorage.ToPublicUrl(comment.User.ImagePath);
            }
        }

        public async Task<List<Comment>> GetCommentsAsync()
        {
            return await _dbContext.Comments.ToListAsync();
        }
    }
}
