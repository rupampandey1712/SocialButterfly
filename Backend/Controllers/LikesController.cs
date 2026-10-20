using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LikesController : ControllerBase
    {
        private readonly ILikeService _like;

        public LikesController(ILikeService like)
        {
            _like = like;
        }


        [HttpPost]
        public async  Task<IActionResult> LikePost([FromQuery]int postId)
        {
            try
            {
                var like = await _like.LikeCountAsync(postId, User.RequireUserId());
                return Ok(like);

            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("toggle")]
        public async Task<IActionResult> ToggleLike([FromQuery] int postId)
        {
            var result = await _like.ToggleLikeAsync(postId, User.RequireUserId());
            return result == null ? NotFound("A valid user and post are required.") : Ok(result);
        }
    }
}
