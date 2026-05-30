using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Services.Classes
{
    public class CommentService : ICommentService
    {
        private readonly ApplicationDbContext _dbContext;

        public CommentService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
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

            return comment;
        }

        public async Task<List<Comment>> GetCommentAsync(int postId)
        {
            return await _dbContext.Comments
                .Include(c => c.User)
                .Where(c => c.PostId == postId)
                .ToListAsync();
        }

        public async Task<List<Comment>> GetCommentsAsync()
        {
            return await _dbContext.Comments.ToListAsync();
        }
    }
}
