using System.Security.Claims;

namespace SocialMediaWeb.Services.Classes
{
    /// <summary>
    /// The caller's identity always comes from the validated JWT, never from ids the
    /// client puts in the URL or body.
    /// </summary>
    public static class CurrentUser
    {
        public static int? GetUserId(this ClaimsPrincipal principal)
        {
            var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var userId) ? userId : null;
        }

        /// <summary>For [Authorize]d actions, where a missing id means a malformed token.</summary>
        public static int RequireUserId(this ClaimsPrincipal principal) =>
            principal.GetUserId() ?? throw new UnauthorizedAccessException("The access token has no user id.");

        public static bool IsAdmin(this ClaimsPrincipal principal) => principal.IsInRole("Admin");
    }
}
