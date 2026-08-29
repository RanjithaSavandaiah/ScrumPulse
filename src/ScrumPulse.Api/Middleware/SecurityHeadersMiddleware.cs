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

        // Content Security Policy
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "font-src 'self' https://fonts.gstatic.com; " +
            "img-src 'self' data: https:; " +
            "connect-src 'self' https:; " +
            "frame-ancestors 'none';";

        // Prevent embedding in cross-origin contexts
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        return next(httpContext);
    }
}
