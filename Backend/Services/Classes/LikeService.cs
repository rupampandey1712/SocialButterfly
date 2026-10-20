using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace SocialMediaWeb.Services.Classes
{
    public class LikeService : ILikeService
    {

        private readonly ApplicationDbContext _context;
        private readonly INotificationPublisher? _notifications;

        public LikeService(ApplicationDbContext context, INotificationPublisher? notifications = null)
        {
            _context = context;
            _notifications = notifications;
        }

        public async Task<int> GetLikeCountAsync(int postId)
        {
            var post = await _context.Posts.FindAsync(postId);

            if (post == null)
                return -1;

            var likeCount = await _context.Likes.CountAsync(l => l.PostId == postId);

            return likeCount;
        }

        public async Task<LikesDto> GiveLikeAsync(LikesDto likes)
        {
            {
                try
                {
                    if (!IsValidFoodData(likes))
                    {
                        throw new ArgumentException("Invalid Food data.");
                    }


                    var post = new Like
                    {
                        UserId = likes.UserId,
                        CreatedAt = likes.CreatedAt,
                        PostId = likes.PostId

                    };


                    var postEntry = await _context.AddAsync(post);
                    await _context.SaveChangesAsync();

                    var postEntity = postEntry.Entity;

                    return new LikesDto
                    {
                        UserId = postEntity.UserId,
                        CreatedAt = postEntity.CreatedAt,
                        PostId = postEntity.PostId
                    };


                }
                catch (Exception ex)
                {
                    throw new ArgumentException(ex.Message);
                }
            }
        }

        public async Task<int> LikeCountAsync(int postId, int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.IsDeleted)
                return -1;

            var post = await _context.Posts.FindAsync(postId);

            if (post == null)
                return -1;

            var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == id);

            if (existingLike != null)
                return await _context.Likes.CountAsync(l => l.PostId == postId);


            var like = new Like
            {
                PostId = postId,
                UserId = id
            };

            _context.Likes.Add(like);
            await _context.SaveChangesAsync();

            var likeCount = await _context.Likes
                .CountAsync(l => l.PostId == postId);


            return likeCount;
        }

        public async Task<LikeToggleResultDto?> ToggleLikeAsync(int postId, int userId)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId && !u.IsDeleted);
            var postExists = await _context.Posts.AnyAsync(p => p.Id == postId);
            if (!userExists || !postExists)
                return null;

            var existingLike = await _context.Likes.FirstOrDefaultAsync(l => l.PostId == postId && l.UserId == userId);

            if (existingLike != null)
            {
                _context.Likes.Remove(existingLike);
            }
            else
            {
                _context.Likes.Add(new Like { PostId = postId, UserId = userId, CreatedAt = DateTime.UtcNow });
            }

            await _context.SaveChangesAsync();

            if (_notifications != null)
            {
                var type = existingLike == null ? NotificationTypes.PostLiked : NotificationTypes.PostUnliked;
                await _notifications.PublishAsync(type, userId, postId: postId);
            }

            return new LikeToggleResultDto
            {
                PostId = postId,
                LikeCount = await _context.Likes.CountAsync(l => l.PostId == postId),
                IsLiked = existingLike == null
            };
        }

        private bool IsValidFoodData(LikesDto dto)
        {
            return true;
        }

    }
}
