using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowsController : ControllerBase
    {
        private readonly IFollowService _follows;

        public FollowsController(IFollowService follows)
        {
            _follows = follows;
        }

        /// <summary>The signed-in user follows or unfollows <paramref name="targetId"/>.</summary>
        [HttpPost("{targetId:int}/toggle")]
        public async Task<ActionResult<FollowResultDto>> Toggle(int targetId)
        {
            try
            {
                return Ok(await _follows.ToggleFollowAsync(User.RequireUserId(), targetId));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{userId:int}/followers")]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> Followers(int userId)
        {
            return Ok(await _follows.GetFollowersAsync(userId, User.GetUserId()));
        }

        [HttpGet("{userId:int}/following")]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> Following(int userId)
        {
            return Ok(await _follows.GetFollowingAsync(userId, User.GetUserId()));
        }

        [HttpGet("people")]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> People([FromQuery] string? q, [FromQuery] int take = 60)
        {
            return Ok(await _follows.GetPeopleAsync(User.GetUserId(), q, take));
        }

        [HttpGet("suggestions")]
        public async Task<ActionResult<IEnumerable<UserListItemDto>>> Suggestions([FromQuery] int take = 5)
        {
            return Ok(await _follows.GetSuggestionsAsync(User.RequireUserId(), take));
        }
    }
}
