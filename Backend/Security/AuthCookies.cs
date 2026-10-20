namespace SocialMediaWeb.Security
{
    /// <summary>
    /// The JWT lives in an HttpOnly cookie so page scripts (and therefore XSS) can never
    /// read it. SameSite=Strict keeps browsers from attaching it to cross-site requests.
    /// </summary>
    public static class AuthCookies
    {
        public const string SessionCookie = "sb_session";

        public static void SetSession(HttpResponse response, string token, DateTimeOffset expires, bool secure)
        {
            response.Cookies.Append(SessionCookie, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = expires,
                IsEssential = true
            });
        }

        public static void ClearSession(HttpResponse response, bool secure)
        {
            response.Cookies.Delete(SessionCookie, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });
        }

        /// <summary>Secure cookies everywhere except plain-HTTP local development.</summary>
        public static bool UseSecureCookies(HttpRequest request, IHostEnvironment environment) =>
            request.IsHttps || !environment.IsDevelopment();
    }
}
