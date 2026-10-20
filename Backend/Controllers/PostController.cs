using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly ISocialMediaPost _post;
        private readonly ICommentService _comment;

        public PostController(ISocialMediaPost post, ICommentService comment)
        {
            _post = post;
            _comment = comment;
        }

        [HttpPost]
        public async Task<ActionResult<SocialMediaPost>> CreatePost([FromForm] PostCreateDto postCreateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Posts are always authored by the signed-in user.
                postCreateDto.UserId = User.RequireUserId();
                var post = await _post.CreatePostAsync(postCreateDto);
                return Ok(post);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Comment>>> GetComments()
        {
            var comments = await _comment.GetCommentsAsync();

            if (comments == null || !comments.Any())
            {
                return NoContent();
            }

            return Ok(comments);
        }

       /* [HttpGet("GetAllPosts")]
        public async Task<ActionResult<Comment>> GetComment()
        {
            var post = await _post.GetAllPostsAsync();

            if (post == null)
            {
                return NotFound();
            }

            return Ok(post);
        }*/

        [HttpPost("Comments")]
        public async Task<ActionResult<Comment>> CreateComment(CreateCommentDto commentCreateDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            commentCreateDto.UserId = User.RequireUserId();
            var comment = await _comment.AddCommentAsync(commentCreateDto);

            return Ok(new { id = comment.Id, comment });
        }

        [HttpGet("UserPost")]
        public async Task<ActionResult<IEnumerable<PostFeedDto>>> UserPost()
        {
            try
            {
                // Projected DTOs: the old entity graph serialized authors' password hashes.
                var post = await _post.GetFeedAsync(User.GetUserId(), 0, 50);
                return Ok(post);
            }
            catch
            {
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [HttpGet("feed")]
        public async Task<ActionResult<IEnumerable<PostFeedDto>>> Feed([FromQuery] int skip = 0, [FromQuery] int take = 20)
        {
            var posts = await _post.GetFeedAsync(User.GetUserId(), skip, take);
            return Ok(posts);
        }

        [HttpGet("user/{userId:int}")]
        public async Task<ActionResult<IEnumerable<PostFeedDto>>> PostsByUser(int userId)
        {
            var posts = await _post.GetUserPostsAsync(userId, User.GetUserId());
            return Ok(posts);
        }

        [HttpGet("details/{postId:int}")]
        public async Task<ActionResult<PostFeedDto>> PostDetails(int postId)
        {
            var post = await _post.GetPostAsync(postId, User.GetUserId());
            return post == null ? NotFound() : Ok(post);
        }

        [HttpDelete("{postId:int}")]
        public async Task<IActionResult> DeletePost(int postId)
        {
            try
            {
                var deleted = await _post.DeletePostAsync(postId, User.RequireUserId());
                return deleted ? NoContent() : NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ex.Message);
            }
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<IEnumerable<PostFeedDto>>> GetSingleUser(int id)
        {
            return Ok(await _post.GetUserPostsAsync(id, User.GetUserId()));
        }
    }
}
