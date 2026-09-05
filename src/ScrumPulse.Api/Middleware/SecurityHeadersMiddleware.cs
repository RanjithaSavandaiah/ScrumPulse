namespace ScrumPulse.Api.Middleware;

/// <summary>
/// Adds OWASP-recommended security headers to all HTTP responses.
/// Critical for a publicly-hosted site.
/// </summary>
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext httpContext)
    {
        var headers = httpContext.Response.Headers;

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking via framing
        headers["X-Frame-Options"] = "DENY";

        // Enable browser XSS protection
        headers["X-XSS-Protection"] = "1; mode=block";

        // Enforce HTTPS via HSTS (1 year)
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";

        // Restrict referrer leakage
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy (Hardened + Google AdSense Whitelisted)
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://pagead2.googlesyndication.com https://adservice.google.com https://www.googletagservices.com https://tpc.googlesyndication.com; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com data:; " +
            "img-src 'self' data: https: https://pagead2.googlesyndication.com; " +
            "connect-src 'self' https: https://pagead2.googlesyndication.com https://googleads.g.doubleclick.net; " +
            "frame-src 'self' https://googleads.g.doubleclick.net https://tpc.googlesyndication.com https://www.google.com; " +
            "frame-ancestors 'none';";

        // Prevent embedding in cross-origin contexts
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        return next(httpContext);
    }
}
