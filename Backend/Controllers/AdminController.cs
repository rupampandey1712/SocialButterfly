using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _service;

        public AdminController(IAdminService service)
        {
            _service = service;
        }


        [HttpGet("PostByDate")]
        public async Task<IActionResult> GetPostsByDay()
        {
            try
            {
                var posts = await _service.GetPostByDate();
                return Ok(posts);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status400BadRequest, $"Error Getting Data: {ex.Message}");
            }
        }

        [HttpGet("GetUserDetail")]
        public async Task<IActionResult> GetUserDetails()
        {
            try
            {
                var users = await _service.GetUserDetail();
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status400BadRequest, $"Error Getting Data: {ex.Message}");
            }
        }

        [HttpPut]
        public async Task<IActionResult> UpdateIsDeletedFlag([FromQuery] int id, [FromQuery] UpdateUserFlag flag)
        {
            try
            {
                await _service.UpdateIsUserDelete(id, flag);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status400BadRequest, $"Error Getting Data: {ex.Message}");
            }
        }
    }
}
