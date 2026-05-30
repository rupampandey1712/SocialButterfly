using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface ILikeService
    {
        Task<LikesDto> GiveLikeAsync(LikesDto likes);

        Task<int> GetLikeCountAsync(int postId);

        Task<int> LikeCountAsync(int postId, int id);
    }
}
