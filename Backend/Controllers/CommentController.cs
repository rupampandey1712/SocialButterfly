using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CommentController : ControllerBase
    {
        private readonly ICommentService _service;
        public CommentController(ICommentService service) { 
        
            _service = service;
        }


        [HttpPost]
        public async Task<ActionResult<CommentResponseDto>> AddCommentToPost(CreateCommentDto commentDto)
        {
            try
            {
                // The author is whoever holds the token, not the userId in the body.
                commentDto.UserId = User.RequireUserId();
                var comment = await _service.AddCommentAsync(commentDto);
                return Ok(comment);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{postId}")]
        public async Task<ActionResult<IEnumerable<CommentResponseDto>>> PostComments(int postId)
        {
            try
            {
                var comments = await _service.GetPostCommentsAsync(postId);
                return Ok(comments);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

    }
}
