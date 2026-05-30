using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Models;
using SocialMediaWeb.Services.Interfaces;
using System.Diagnostics.Metrics;

namespace SocialMediaWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUserService _userService;
        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] UserRegistrationDto registrationDto)
        {
            try
            {
                await _userService.RegisterAsync(registrationDto);
                return Ok(new { message = "User registered successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm]UserLoginDto authenticationDto)
        {
            try
            {
                var userResponseDto = await _userService.LoginAsync(authenticationDto);
                return Ok(userResponseDto);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status401Unauthorized, $"Error Logging in user: {ex.Message}");
            }
        }

        [HttpPut]
        public async Task<ActionResult<User>> UpdateUser([FromForm] UserUpdateDto userUpdateDto)
        {
            try
            {
                var user = await _userService.UpdateUserAsync(userUpdateDto);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetSingleUser(int id)
        {
            try
            {
                var user = await _userService.FindById(id);
                return Ok(user);
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }

        }

        [HttpGet("Get")]
        public async Task<ActionResult<User>> GetUsers()
        {
            try
            {
                var user = await _userService.GetUserAsync();
                return Ok(user);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"No User Found {ex.Message}");
            }

        }


        [HttpGet("Search")]
        public async Task<IActionResult> Search([FromQuery]UserDto dto)
        {
            try
            {
                var user = await _userService.Search(dto).ToListAsync();
                return Ok(user);
            }
             catch (Exception ex)
             {
                 return StatusCode(StatusCodes.Status400BadRequest, $"Error updating user: {ex.Message}");
             }
        }
    }
}
