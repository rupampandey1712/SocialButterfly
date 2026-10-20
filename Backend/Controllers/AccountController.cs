using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using SocialMediaWeb.Dtos;
using SocialMediaWeb.Security;
using SocialMediaWeb.Services.Classes;
using SocialMediaWeb.Services.Interfaces;

namespace SocialMediaWeb.Controllers
{
    /// <summary>
    /// Sign-up, sign-in, verification codes and account settings. Everything except the
    /// sign-in/sign-up flow requires a JWT, and "my account" actions only ever act on
    /// the user in the token.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IBlobStorageService _blobStorage;
        private readonly IVerificationService _verification;
        private readonly IHostEnvironment _environment;
        private readonly BotGate _botGate;

        public AccountController(
            IUserService userService,
            IBlobStorageService blobStorage,
            IVerificationService verification,
            IHostEnvironment environment,
            BotGate botGate)
        {
            _userService = userService;
            _blobStorage = blobStorage;
            _verification = verification;
            _environment = environment;
            _botGate = botGate;
        }

        private string ClientIp => BotGate.ClientIp(HttpContext);

        /// <summary>
        /// Puts the JWT in the HttpOnly session cookie and strips it from the JSON body,
        /// so the token is never exposed to page scripts.
        /// </summary>
        private UserResponseDto SignIn(UserResponseDto session)
        {
            var token = session.Token!;
            var expires = new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
            AuthCookies.SetSession(Response, token, new DateTimeOffset(expires, TimeSpan.Zero),
                AuthCookies.UseSecureCookies(Request, _environment));
            session.Token = null;
            return session;
        }

        /// <summary>Who is signed in (from the cookie). Called by the app on startup; also issues the CSRF cookie.</summary>
        [AllowAnonymous]
        [HttpGet("session")]
        public async Task<IActionResult> Session()
        {
            var userId = User.GetUserId();
            if (userId == null)
            {
                return Ok(new { authenticated = false });
            }

            try
            {
                var settings = await _userService.GetSettingsAsync(userId.Value);
                return Ok(new
                {
                    authenticated = true,
                    id = settings.Id,
                    name = settings.Name,
                    username = settings.Username,
                    imagePath = settings.ImagePath,
                    theme = settings.Theme,
                    role = User.IsAdmin() ? "Admin" : "User"
                });
            }
            catch (KeyNotFoundException)
            {
                // The account was deleted while the cookie was still valid.
                AuthCookies.ClearSession(Response, AuthCookies.UseSecureCookies(Request, _environment));
                return Ok(new { authenticated = false });
            }
        }

        [AllowAnonymous]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            AuthCookies.ClearSession(Response, AuthCookies.UseSecureCookies(Request, _environment));
            return NoContent();
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] UserRegistrationDto registrationDto)
        {
            if (_botGate.IsHoneypotTripped(HttpContext, registrationDto.Website))
            {
                return BadRequest("We couldn't create your account. Please try again.");
            }

            if (_botGate.Check(HttpContext, "register", null) is { } challenge)
            {
                return challenge;
            }

            try
            {
                var result = await _userService.RegisterWithResultAsync(registrationDto);
                return Ok(result);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] UserLoginDto authenticationDto)
        {
            var accountKey = BotDefense.AccountKey(authenticationDto.Identifier ?? authenticationDto.Email);
            const string genericFailure = "Sorry, your password was incorrect. Please double-check your password.";

            if (_botGate.IsHoneypotTripped(HttpContext, authenticationDto.Website))
            {
                // Looks like an ordinary failure so the bot learns nothing.
                _botGate.Defense.RecordFailure(ClientIp, accountKey);
                return StatusCode(StatusCodes.Status401Unauthorized, genericFailure);
            }

            if (_botGate.Check(HttpContext, "login", accountKey) is { } challenge)
            {
                return challenge;
            }

            try
            {
                var userResponseDto = await _userService.LoginAsync(authenticationDto);
                _botGate.Defense.RecordSuccess(ClientIp, accountKey);
                return Ok(SignIn(userResponseDto));
            }
            catch (Exception ex)
            {
                _botGate.Defense.RecordFailure(ClientIp, accountKey);
                return StatusCode(StatusCodes.Status401Unauthorized, ex is ArgumentException ? ex.Message : genericFailure);
            }
        }

        [AllowAnonymous]
        [HttpGet("availability")]
        public async Task<ActionResult<AvailabilityDto>> Availability(
            [FromQuery] string? username, [FromQuery] string? email, [FromQuery] string? phone, [FromQuery] string? name)
        {
            return Ok(await _userService.CheckAvailabilityAsync(username, email, phone, name));
        }

        /// <summary>Sends a one-time code by email or SMS (via Service Bus -> Functions).</summary>
        [AllowAnonymous]
        [EnableRateLimiting("codes")]
        [HttpPost("code/send")]
        public async Task<ActionResult<SendCodeResponseDto>> SendCode([FromBody] SendCodeRequestDto request)
        {
            if (_botGate.IsHoneypotTripped(HttpContext, request.Website))
            {
                return BadRequest("We couldn't send a code right now.");
            }

            if (_botGate.Check(HttpContext, "code-send", BotDefense.AccountKey(request.Target)) is { } challenge)
            {
                return challenge;
            }

            try
            {
                return Ok(await _verification.SendCodeAsync(request));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Verifies a code. Login/Reset purposes return a signed-in session.</summary>
        [AllowAnonymous]
        [EnableRateLimiting("codes")]
        [HttpPost("code/confirm")]
        public async Task<IActionResult> ConfirmCode([FromBody] ConfirmCodeRequestDto request)
        {
            var accountKey = BotDefense.AccountKey(request.Target);
            if (_botGate.IsHoneypotTripped(HttpContext, request.Website))
            {
                return StatusCode(StatusCodes.Status401Unauthorized, "That code isn't valid. Check it and try again.");
            }

            if (_botGate.Check(HttpContext, "code-confirm", accountKey) is { } challenge)
            {
                return challenge;
            }

            try
            {
                var session = await _verification.ConfirmCodeAsync(request);
                _botGate.Defense.RecordSuccess(ClientIp, accountKey);
                return session == null ? Ok(new { verified = true }) : Ok(SignIn(session));
            }
            catch (UnauthorizedAccessException ex)
            {
                _botGate.Defense.RecordFailure(ClientIp, accountKey);
                return StatusCode(StatusCodes.Status401Unauthorized, ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("me/settings")]
        public async Task<ActionResult<UserSettingsDto>> GetSettings()
        {
            try
            {
                return Ok(await _userService.GetSettingsAsync(User.RequireUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPut("me/settings")]
        public async Task<ActionResult<UserSettingsDto>> UpdateSettings([FromForm] UpdateSettingsDto dto)
        {
            try
            {
                return Ok(await _userService.UpdateSettingsAsync(User.RequireUserId(), dto));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return BadRequest(ex.Message);
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPut("me/password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            try
            {
                await _userService.ChangePasswordAsync(User.RequireUserId(), dto);
                return NoContent();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or ArgumentException)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Legacy profile update; only the signed-in user (or an admin) can update an account.</summary>
        [HttpPut]
        public async Task<ActionResult<UserSettingsDto>> UpdateUser([FromForm] UserUpdateDto userUpdateDto)
        {
            if (userUpdateDto.Id != User.RequireUserId() && !User.IsAdmin())
            {
                return Forbid();
            }

            try
            {
                var user = await _userService.UpdateUserAsync(userUpdateDto);
                return Ok(await _userService.GetSettingsAsync(user.Id));
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>Public profile of any member. Never includes credentials or contact details.</summary>
        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserProfileDto>> GetSingleUser(int id)
        {
            return await GetProfile(id);
        }

        [HttpGet("{id:int}/profile")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(int id)
        {
            try
            {
                return Ok(await _userService.GetProfileAsync(id, User.GetUserId()));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>Member directory (used by chat contacts). Emails and birthdays are admin-only.</summary>
        [HttpGet("Get")]
        public async Task<ActionResult<IEnumerable<ResponseDto>>> GetUsers()
        {
            var users = await _userService.GetUserAsync();
            if (!User.IsAdmin())
            {
                foreach (var u in users)
                {
                    u.Email = null;
                    u.Dob = default;
                }
            }

            return Ok(users);
        }

        [HttpGet("Search")]
        public async Task<IActionResult> Search([FromQuery] UserDto dto)
        {
            try
            {
                var users = await _userService.Search(dto).ToListAsync();
                var isAdmin = User.IsAdmin();
                foreach (var u in users)
                {
                    u.ImagePath = _blobStorage.ToPublicUrl(u.ImagePath);
                    if (!isAdmin) u.Email = null;
                }
                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status400BadRequest, $"Error searching user: {ex.Message}");
            }
        }
    }
}
