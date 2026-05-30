using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        public async  Task<IActionResult> LikePost([FromQuery]int postId, [FromQuery] int userId)
        {
            try
            {
                var like = await _like.LikeCountAsync(postId, userId);
                return Ok(like);

            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
