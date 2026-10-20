using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface ISocialMediaPost
    {
        Task<SocialMediaPost> CreatePostAsync(PostCreateDto createDto);

/*        Task<List<SocialMediaPost>> GetAllPostsAsync();*/

        Task<List<SocialMediaPost>> AllPost();

        Task<List<SocialMediaPost>> GetUserpost(int id);

        Task<List<PostFeedDto>> GetFeedAsync(int? viewerId, int skip, int take);

        Task<List<PostFeedDto>> GetUserPostsAsync(int userId, int? viewerId);

        Task<PostFeedDto?> GetPostAsync(int postId, int? viewerId);

        Task<bool> DeletePostAsync(int postId, int userId);

    }
}
