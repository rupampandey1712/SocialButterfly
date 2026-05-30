using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IAdminService
    {
        Task<List<PostScheduleDto>> GetPostByDate();

        Task<IEnumerable<UserScheduleDto>> GetUserDetail();

        Task UpdateIsUserDelete(int id, UpdateUserFlag flag);
    }
}
