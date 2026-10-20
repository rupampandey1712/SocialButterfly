using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _stories;

        public StoriesController(IStoryService stories)
        {
            _stories = stories;
        }

        [HttpPost]
        [RequestSizeLimit(80L * 1024 * 1024)]
        public async Task<ActionResult<StoryItemDto>> Create(
            IFormFile? media, [FromForm] string? caption,
            IFormFile? music, [FromForm] string? musicTitle, [FromForm] string? musicArtist)
        {
            try
            {
                return Ok(await _stories.CreateAsync(User.RequireUserId(), media, caption, music, musicTitle, musicArtist));
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

        [HttpGet("feed")]
        public async Task<ActionResult<IEnumerable<StoryGroupDto>>> Feed()
        {
            return Ok(await _stories.GetFeedAsync(User.RequireUserId()));
        }

        [HttpGet("user/{userId:int}")]
        public async Task<ActionResult<IEnumerable<StoryGroupDto>>> ByUser(int userId)
        {
            return Ok(await _stories.GetUserStoriesAsync(userId, User.RequireUserId()));
        }

        [HttpPost("{storyId:int}/view")]
        public async Task<IActionResult> View(int storyId)
        {
            await _stories.MarkViewedAsync(storyId, User.RequireUserId());
            return NoContent();
        }

        [HttpGet("{storyId:int}/viewers")]
        public async Task<ActionResult<IEnumerable<StoryViewerDto>>> Viewers(int storyId)
        {
            try
            {
                return Ok(await _stories.GetViewersAsync(storyId, User.RequireUserId()));
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        [HttpDelete("{storyId:int}")]
        public async Task<IActionResult> Delete(int storyId)
        {
            try
            {
                return await _stories.DeleteAsync(storyId, User.RequireUserId()) ? NoContent() : NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }
    }
}
