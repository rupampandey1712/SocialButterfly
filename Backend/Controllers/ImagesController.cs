using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    /// <summary>
    /// AI image studio backed by ComfyUI. Generation is asynchronous: POST returns a queued
    /// job, the client polls GET /api/Images/{id} until it succeeds or fails.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        private readonly IImageGenerationService _images;
        private readonly ComfyUiClient _comfy;

        public ImagesController(IImageGenerationService images, ComfyUiClient comfy)
        {
            _images = images;
            _comfy = comfy;
        }

        [HttpGet("status")]
        public async Task<ActionResult<ImageStudioStatusDto>> Status(CancellationToken cancellationToken)
        {
            var userId = User.RequireUserId();
            return Ok(new ImageStudioStatusDto
            {
                Online = await _comfy.IsOnlineAsync(cancellationToken),
                ActiveJobs = await _images.CountActiveAsync(userId),
                RemainingToday = Math.Max(ImageGenerationService.MaxJobsPerDay - await _images.UsedTodayAsync(userId), 0),
                Styles = ImageGenerationService.Styles.Keys.ToArray()
            });
        }

        [HttpPost]
        [EnableRateLimiting("ai")]
        public async Task<ActionResult<GeneratedImageDto>> Generate([FromBody] GenerateImageRequestDto request)
        {
            try
            {
                return Ok(await _images.CreateAsync(User.RequireUserId(), request));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<GeneratedImageDto>> Get(int id)
        {
            var job = await _images.GetAsync(id, User.RequireUserId());
            return job == null ? NotFound() : Ok(job);
        }

        [HttpGet("mine")]
        public async Task<ActionResult<IEnumerable<GeneratedImageDto>>> Mine([FromQuery] int take = 24)
        {
            return Ok(await _images.ListMineAsync(User.RequireUserId(), take));
        }

        [HttpPost("{id:int}/share")]
        public async Task<IActionResult> Share(int id, [FromBody] ShareGeneratedImageDto request)
        {
            try
            {
                var postId = await _images.ShareAsPostAsync(id, User.RequireUserId(), request.Caption);
                return Ok(new { postId });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
