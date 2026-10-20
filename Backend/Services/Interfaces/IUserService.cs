using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface IUserService
    {
        Task RegisterAsync(UserRegistrationDto registrationDto);

        Task<UserResponseDto> LoginAsync(UserLoginDto loginDto);
        
        Task<User> UpdateUserAsync(UserUpdateDto userUpdateDto);
        
        Task<User> FindById(int id);

        Task<UserProfileDto> GetProfileAsync(int userId, int? viewerId);

        Task<RegisterResponseDto> RegisterWithResultAsync(UserRegistrationDto registrationDto);

        Task<User?> FindByIdentifierAsync(string identifier);

        UserResponseDto ToLoginResponse(User user);

        Task<AvailabilityDto> CheckAvailabilityAsync(string? username, string? email, string? phone, string? name);

        Task<UserSettingsDto> GetSettingsAsync(int userId);

        Task<UserSettingsDto> UpdateSettingsAsync(int userId, UpdateSettingsDto dto);

        Task ChangePasswordAsync(int userId, ChangePasswordDto dto);
        
        Task<List<ResponseDto>> GetUserAsync();

        IQueryable<UserDto> Search(UserDto dto);
    }
}
