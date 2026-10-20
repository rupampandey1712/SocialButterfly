using System.Security.Cryptography;
using System.Text;

namespace SocialMediaWeb.Security
{
    /// <summary>
    /// CSRF defence for cookie-authenticated API calls, in two layers:
    /// 1. Origin check: state-changing requests that carry an Origin/Referer must come
    ///    from one of the configured frontend origins.
    /// 2. Double-submit token: a random XSRF-TOKEN cookie (readable by our own scripts,
    ///    not by other sites) must be echoed in the X-XSRF-TOKEN header. A forged
    ///    cross-site form or fetch cannot read the cookie, so it cannot send the header.
    /// Requests authenticated with an explicit Authorization header (API tools, Swagger)
    /// are not CSRF-able and skip the token check, but not the origin check.
    /// </summary>
    public sealed class CsrfProtectionMiddleware
    {
        public const string CookieName = "XSRF-TOKEN";
        public const string HeaderName = "X-XSRF-TOKEN";

        private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase)
        {
            "GET", "HEAD", "OPTIONS", "TRACE"
        };

        private readonly RequestDelegate _next;
        private readonly HashSet<string> _allowedOrigins;
        private readonly IHostEnvironment _environment;

        public CsrfProtectionMiddleware(RequestDelegate next, IConfiguration configuration, IHostEnvironment environment)
        {
            _next = next;
            _environment = environment;
            _allowedOrigins = AllowedOrigins.From(configuration);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                await _next(context);
                return;
            }

            var cookieToken = context.Request.Cookies[CookieName];
            if (string.IsNullOrEmpty(cookieToken))
            {
                // Issue a token on the first API call (the app calls GET /api/Account/session at startup).
                IssueToken(context);
            }

            if (!SafeMethods.Contains(context.Request.Method))
            {
                if (!IsAllowedOrigin(context.Request))
                {
                    await Reject(context, "Cross-site request blocked.");
                    return;
                }

                var usesBearerHeader = context.Request.Headers.Authorization.ToString()
                    .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                if (!usesBearerHeader && !TokensMatch(cookieToken, context.Request.Headers[HeaderName].ToString()))
                {
                    await Reject(context, "Missing or invalid CSRF token. Reload the page and try again.");
                    return;
                }
            }

            await _next(context);
        }

        private void IssueToken(HttpContext context)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            context.Response.Cookies.Append(CookieName, token, new CookieOptions
            {
                // Readable by our own page so axios can copy it into the header.
                HttpOnly = false,
                Secure = AuthCookies.UseSecureCookies(context.Request, _environment),
                SameSite = SameSiteMode.Strict,
                Path = "/",
                IsEssential = true
            });
        }

        private bool IsAllowedOrigin(HttpRequest request)
        {
            var origin = request.Headers.Origin.ToString();
            if (string.IsNullOrEmpty(origin) && Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer))
            {
                origin = referer.GetLeftPart(UriPartial.Authority);
            }

            // Non-browser clients send neither header; the auth + token checks still apply to them.
            if (string.IsNullOrEmpty(origin))
            {
                return true;
            }

            var sameOrigin = $"{request.Scheme}://{request.Host}";
            return string.Equals(origin, sameOrigin, StringComparison.OrdinalIgnoreCase) || _allowedOrigins.Contains(origin);
        }

        private static bool TokensMatch(string? cookieToken, string headerToken)
        {
            if (string.IsNullOrEmpty(cookieToken) || string.IsNullOrEmpty(headerToken))
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookieToken), Encoding.UTF8.GetBytes(headerToken));
        }

        private static async Task Reject(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }
    }

    public static class AllowedOrigins
    {
        /// <summary>Frontend origins allowed to call the API (Security:AllowedOrigins).</summary>
        public static HashSet<string> From(IConfiguration configuration) =>
            (configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>())
                .Select(origin => origin.Trim().TrimEnd('/'))
                .Where(origin => origin.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
