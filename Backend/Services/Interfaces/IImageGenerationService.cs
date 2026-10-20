using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IImageGenerationService
    {
        Task<GeneratedImageDto> CreateAsync(int userId, GenerateImageRequestDto request);

        /// <summary>Only returns the job if it belongs to <paramref name="userId"/>.</summary>
        Task<GeneratedImageDto?> GetAsync(int id, int userId);

        Task<List<GeneratedImageDto>> ListMineAsync(int userId, int take);

        Task<int> CountActiveAsync(int userId);

        Task<int> UsedTodayAsync(int userId);

        /// <summary>Publishes a finished image as a post (reusing the blob). Returns the post id.</summary>
        Task<int> ShareAsPostAsync(int id, int userId, string? caption);
    }
}
