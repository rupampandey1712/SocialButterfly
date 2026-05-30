using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;

using Microsoft.EntityFrameworkCore;

namespace SocialMediaWeb.Services.Classes
{
    public class LikeService : ILikeService
    {

        private readonly ApplicationDbContext _context;
       

        public LikeService(ApplicationDbContext context)
        {
            _context = context;
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

        private bool IsValidFoodData(LikesDto dto)
        {
            return true;
        }

    }
}
