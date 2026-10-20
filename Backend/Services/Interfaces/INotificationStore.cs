using SocialMediaWeb.Dtos;

namespace SocialMediaWeb.Services.Interfaces
{
    public interface INotificationStore
    {
        /// <summary>Consumer side: fans a queued event out into notification rows.</summary>
        Task ProcessAsync(NotificationEvent notification, CancellationToken cancellationToken = default);

        Task<List<NotificationDto>> GetForUserAsync(int userId, int take);

        Task<int> GetUnreadCountAsync(int userId);

        Task MarkAllReadAsync(int userId);
    }
}
