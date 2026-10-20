using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IFollowService
    {
        Task<FollowResultDto> ToggleFollowAsync(int followerId, int targetId);

        Task<List<UserListItemDto>> GetFollowersAsync(int userId, int? viewerId);

        Task<List<UserListItemDto>> GetFollowingAsync(int userId, int? viewerId);

        Task<List<UserListItemDto>> GetPeopleAsync(int? viewerId, string? query, int take);

        Task<List<UserListItemDto>> GetSuggestionsAsync(int viewerId, int take);
    }
}
