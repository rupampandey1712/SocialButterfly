namespace SocialMediaWeb.Security
{
    /// <summary>
    /// Browser hardening headers. The Content-Security-Policy is the main XSS backstop:
    /// only our own scripts run, nothing can frame the app, and media may only come from
    /// our origin and the blob storage endpoint.
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _pageCsp;

        public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            var blobOrigin = BlobOrigin(configuration["Blob:PublicBaseUrl"]);
            _pageCsp = string.Join("; ",
                "default-src 'self'",
                "script-src 'self' https://maps.googleapis.com",
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com",
                "font-src 'self' data: https://fonts.gstatic.com",
                $"img-src 'self' data: blob: {blobOrigin} https://*.googleapis.com https://*.gstatic.com",
                $"media-src 'self' blob: {blobOrigin}",
                "connect-src 'self' https://api.iconify.design https://api.simplesvg.com https://api.unisvg.com https://maps.googleapis.com",
                "object-src 'none'",
                "base-uri 'self'",
                "form-action 'self'",
                "frame-ancestors 'none'");
        }

        public Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
                headers["Cross-Origin-Opener-Policy"] = "same-origin";
                headers["Content-Security-Policy"] = context.Request.Path.StartsWithSegments("/api")
                    ? "default-src 'none'; frame-ancestors 'none'"
                    : _pageCsp;
                return Task.CompletedTask;
            });

            return _next(context);
        }

        private static string BlobOrigin(string? publicBaseUrl) =>
            Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var uri)
                ? uri.GetLeftPart(UriPartial.Authority)
                : "http://localhost:10000";
    }
}
