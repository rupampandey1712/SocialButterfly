using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    /// <summary>
    /// Read side of the activity feed. Rows are written asynchronously by the
    /// Functions worker consuming the post-notifications and friend-requests queues.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class NotificationsController : ControllerBase
    {
        private readonly INotificationStore _notifications;

        public NotificationsController(INotificationStore notifications)
        {
            _notifications = notifications;
        }

        [HttpGet("me")]
        public async Task<ActionResult<IEnumerable<NotificationDto>>> GetNotifications([FromQuery] int take = 30)
        {
            return Ok(await _notifications.GetForUserAsync(User.RequireUserId(), take));
        }

        [HttpGet("me/unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            return Ok(new { count = await _notifications.GetUnreadCountAsync(User.RequireUserId()) });
        }

        [HttpPost("me/read")]
        public async Task<IActionResult> MarkAllRead()
        {
            await _notifications.MarkAllReadAsync(User.RequireUserId());
            return NoContent();
        }
    }
}
