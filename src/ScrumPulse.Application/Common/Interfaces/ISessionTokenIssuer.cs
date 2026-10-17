namespace ScrumPulse.Application.Common.Interfaces;

using ScrumPulse.Application.Common.Security;

/// <summary>
/// Issues the signed, expiring tokens that carry a caller's team and role between requests.
/// </summary>
/// <remarks>
/// The token is the only thing the API trusts for tenant and role. Request headers such as
/// X-Team-Id and X-User-Role are caller-controlled and are deliberately not honoured.
/// </remarks>
public interface ISessionTokenIssuer
{
    /// <summary>
    /// Signs a session and returns the token string to hand back to the browser.
    /// </summary>
    /// <param name="session">Team and role the server has decided to grant.</param>
    /// <param name="lifetime">How long the token stays valid.</param>
    string IssueToken(TeamSession session, TimeSpan lifetime);
}
