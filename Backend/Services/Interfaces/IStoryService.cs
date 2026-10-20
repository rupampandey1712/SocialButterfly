using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IStoryService
    {
        Task<StoryItemDto> CreateAsync(int userId, IFormFile? media, string? caption,
            IFormFile? music = null, string? musicTitle = null, string? musicArtist = null);

        /// <summary>Active stories from the viewer, people they follow and friends, grouped per author.</summary>
        Task<List<StoryGroupDto>> GetFeedAsync(int viewerId);

        Task<List<StoryGroupDto>> GetUserStoriesAsync(int userId, int viewerId);

        Task MarkViewedAsync(int storyId, int viewerId);

        Task<List<StoryViewerDto>> GetViewersAsync(int storyId, int ownerId);

        Task<bool> DeleteAsync(int storyId, int userId);

        /// <summary>Deletes expired stories and their blobs. Returns how many were removed.</summary>
        Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
    }
}
