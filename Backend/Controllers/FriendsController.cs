using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    /// <summary>
    /// Mutual friendships. The signed-in user (from the JWT) is always one side of any
    /// change, and pending requests are only visible to their receiver.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class FriendsController : ControllerBase
    {
        private readonly IFriendService _friend;
        private readonly IBlobStorageService _blobStorage;

        public FriendsController(IFriendService friend, IBlobStorageService blobStorage)
        {
            _friend = friend;
            _blobStorage = blobStorage;
        }

        [HttpPost]
        public async Task<ActionResult<FriendDto>> SendfriendRequest(FriendDto friends)
        {
            try
            {
                friends.SenderId = User.RequireUserId();
                var friend = await _friend.SendFriendRequest(friends);
                return Ok(friend);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Accepts a request <paramref name="senderId"/> sent to the signed-in user.</summary>
        [HttpPost("{senderId:int}")]
        public async Task<ActionResult<FriendDto>> AcceptFriendRequest(int senderId)
        {
            try
            {
                var friend = await _friend.AcceptFriendRequest(senderId, User.RequireUserId());
                return Ok(friend);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Kept for older clients; the receiver must be the signed-in user.</summary>
        [HttpPost("{senderId:int}/{receiverId:int}")]
        public async Task<ActionResult<FriendDto>> AcceptFriendRequestWithReceiver(int senderId, int receiverId)
        {
            if (receiverId != User.RequireUserId())
            {
                return Forbid();
            }

            return await AcceptFriendRequest(senderId);
        }

        /// <summary>Declines, cancels or removes a friendship. The signed-in user must be one side of it.</summary>
        [HttpDelete("{senderId:int}/{receiverId:int}")]
        public async Task<IActionResult> DeleteOrDeclineFriendRequest(int senderId, int receiverId)
        {
            var currentUserId = User.RequireUserId();
            if (currentUserId != senderId && currentUserId != receiverId)
            {
                return Forbid();
            }

            try
            {
                var deleted = await _friend.DeleteOrDeclineFriendRequest(senderId, receiverId);
                return Ok(new { success = deleted });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Status between the signed-in user and <paramref name="otherUserId"/>.</summary>
        [HttpGet("status/{otherUserId:int}")]
        public async Task<IActionResult> GetFriendshipStatus(int otherUserId)
        {
            var status = await _friend.GetFriendshipStatus(User.RequireUserId(), otherUserId);
            return Ok(new { status });
        }

        [HttpGet("{userId:int}/friends")]
        public async Task<ActionResult<IEnumerable<PostAuthorDto>>> GetAllFriends(int userId)
        {
            try
            {
                // Project to public fields only: the service returns full User entities
                // (password hash, email, phone), which must never be serialized.
                var result = await _friend.GetFriends(userId);
                var friends = (result.Value ?? Enumerable.Empty<SocialMediaWeb.Models.User>())
                    .Select(u => new PostAuthorDto { Id = u.Id, Name = u.Name, ImagePath = _blobStorage.ToPublicUrl(u.ImagePath) })
                    .ToList();
                return Ok(friends);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Number of pending requests for the signed-in user.</summary>
        [HttpGet("me/requests")]
        public async Task<ActionResult<FriendDto>> Requests()
        {
            return Ok(await _friend.GetRequest(User.RequireUserId()));
        }

        /// <summary>People who sent the signed-in user a request.</summary>
        [HttpGet("me/requestuser")]
        public async Task<ActionResult<FriendDto>> RequestUser()
        {
            return Ok(await _friend.GetUserRequest(User.RequireUserId()));
        }

        /// <summary>Friends of any member (shown on profiles).</summary>
        [HttpGet("{userId:int}/myfriends")]
        public async Task<ActionResult<FriendDto>> MyFriends(int userId)
        {
            try
            {
                var friend = await _friend.GetMyFriends(userId);
                return Ok(friend);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{userId:int}/friendscount")]
        public async Task<ActionResult<FriendDto>> MyFriendsCount(int userId)
        {
            try
            {
                var friend = await _friend.FriendsCount(userId);
                return Ok(friend);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
