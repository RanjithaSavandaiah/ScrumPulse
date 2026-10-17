namespace ScrumPulse.Api.Security;

/// <summary>
/// Defines the cookie that carries the session token, and the flags that keep it safe.
/// </summary>
/// <remarks>
/// A cookie is used rather than local storage so that script running on the page cannot read
/// the token. The previous design kept the role in local storage, where anyone could open dev
/// tools and promote themselves to Scrum Master.
/// </remarks>
public static class SessionCookie
{
    /// <summary>
    /// Cookie name. The <c>__Host-</c> prefix is enforced by browsers: the cookie is only
    /// accepted when it is Secure, path is /, and no Domain is set, which stops a
    /// subdomain from overwriting it.
    /// </summary>
    public const string Name = "__Host-ScrumPulseSession";

    /// <summary>Cookie name used when the request is plain HTTP, such as local development.</summary>
    /// <remarks>
    /// Browsers reject a <c>__Host-</c> cookie that is not marked Secure, so over HTTP the
    /// prefix has to be dropped or local development cannot hold a session at all.
    /// </remarks>
    public const string InsecureName = "ScrumPulseSession";

    /// <summary>Returns the cookie name appropriate to the scheme the request arrived on.</summary>
    public static string ResolveName(bool isHttps) => isHttps ? Name : InsecureName;

    /// <summary>
    /// Builds the cookie options for issuing or clearing the session cookie.
    /// </summary>
    /// <param name="isHttps">Whether the current request arrived over HTTPS.</param>
    /// <param name="expiresAt">Absolute expiry, or null to create a session cookie.</param>
    public static CookieOptions BuildOptions(bool isHttps, DateTimeOffset? expiresAt) => new()
    {
        // Keeps the token out of reach of any script on the page, so an XSS bug cannot
        // exfiltrate a Scrum Master session.
        HttpOnly = true,

        // Never send the token over plain HTTP in a deployed environment.
        Secure = isHttps,

        // The API is same-origin with the SPA, so the cookie never needs to travel
        // cross-site. Strict also means the session is not attached to requests triggered
        // from another site, which removes the CSRF exposure on the mutating endpoints.
        SameSite = SameSiteMode.Strict,

        Path = "/",
        Expires = expiresAt,
        IsEssential = true
    };
}
