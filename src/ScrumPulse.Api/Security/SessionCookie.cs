namespace ScrumPulse.Api.Security;

public static class SessionCookie
{
    // __Host- is browser-enforced: accepted only when Secure, path /, and no Domain, so a
    // subdomain cannot overwrite it. Plain-HTTP local dev has to drop the prefix.
    public const string Name = "__Host-ScrumPulseSession";

    public const string InsecureName = "ScrumPulseSession";

    public static string ResolveName(bool isHttps) => isHttps ? Name : InsecureName;

    public static CookieOptions BuildOptions(bool isHttps, DateTimeOffset? expiresAt) => new()
    {
        // Keeps the token away from page script, so an XSS bug cannot exfiltrate a session.
        HttpOnly = true,

        Secure = isHttps,

        // Same-origin with the SPA, so Strict is free and kills the CSRF exposure on writes.
        SameSite = SameSiteMode.Strict,

        Path = "/",
        Expires = expiresAt,
        IsEssential = true
    };
}
