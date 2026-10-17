namespace ScrumPulse.Api.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Api.Security;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Common.Security;
using ScrumPulse.Domain.Enums;

/// <summary>Request to join a squad using the code its Scrum Master shared.</summary>
/// <param name="JoinCode">Squad join code. Acts as the shared secret granting membership.</param>
/// <param name="DisplayName">Name recorded against anything this member creates.</param>
public record JoinSquadRequest(
    [property: Required(ErrorMessage = "Enter the squad join code.")]
    string? JoinCode,
    [property: Required(ErrorMessage = "Enter your name so your updates are attributed to you.")]
    [property: StringLength(60, MinimumLength = 2, ErrorMessage = "Name must be between 2 and 60 characters.")]
    string? DisplayName);

/// <summary>Request to raise an existing squad session to Scrum Master.</summary>
public record VerifyPinRequest(
    [property: Required(ErrorMessage = "Enter the Scrum Master PIN.")]
    string? Pin);

/// <summary>What the caller is currently allowed to do. Drives which controls the UI renders.</summary>
public record SessionStateResponse(bool IsAuthenticated, Guid? TeamId, string? TeamName, string Role, string? DisplayName);

/// <summary>
/// Issues and elevates the session tokens that carry team and role between requests.
/// </summary>
/// <remarks>
/// <para>
/// There are two secrets. The squad join code grants membership and a Developer role; the
/// Scrum Master PIN raises an existing membership to a privileged role. Both are checked
/// server side and the outcome is recorded in a signed token, so a caller cannot grant
/// themselves either by editing a header or local storage.
/// </para>
/// <para>
/// This is the seam Google sign-in will replace. When it lands the identity provider supplies
/// the member, and this controller keeps issuing the same session token, so nothing
/// downstream has to change.
/// </para>
/// </remarks>
[EnableRateLimiting("auth")]
public sealed class AuthController : BaseApiController
{
    /// <summary>
    /// How long a session lasts. Short enough that a leaked token dies within a working day,
    /// long enough to survive a standup without re-entry.
    /// </summary>
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    private readonly IAppDbContext _dbContext;
    private readonly ISessionTokenIssuer _tokenIssuer;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAppDbContext dbContext,
        ISessionTokenIssuer tokenIssuer,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _tokenIssuer = tokenIssuer;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Exchanges a squad join code for a member session.</summary>
    /// <remarks>
    /// Anonymous by necessity: this is how a caller obtains their first session. The join
    /// code is the secret being checked, and the strict auth rate limiter bounds guessing.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("join")]
    [ProducesResponseType(typeof(SessionStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionStateResponse>> Join(
        [FromBody] JoinSquadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var normalizedJoinCode = request.JoinCode!.Trim();

        // Teams are exempt from the tenant query filter, so this lookup can find a squad the
        // caller is not yet scoped to. That is the whole point of the join code: it is the
        // only thing between a visitor and the squad, which is why this endpoint sits behind
        // the strict auth rate limiter.
        var team = await _dbContext.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate => candidate.JoinCode == normalizedJoinCode && candidate.IsActive,
                cancellationToken);

        if (team is null)
        {
            // The attempted code is deliberately not logged, so the log cannot be mined for
            // near-miss codes.
            _logger.LogWarning(
                "Squad join rejected: no active squad matches the supplied code. Client {ClientIp}",
                HttpContext.Connection.RemoteIpAddress);

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Squad not found",
                Detail = "That join code does not match an active squad."
            });
        }

        var session = new TeamSession(team.Id, RoleType.Developer, request.DisplayName!.Trim());
        IssueSessionCookie(session);

        _logger.LogInformation("Squad session issued for team {TeamId} with role {Role}", team.Id, session.Role);

        return Ok(new SessionStateResponse(true, team.Id, team.Name, session.Role.ToString(), session.DisplayName));
    }

    /// <summary>Raises the current squad session to Scrum Master on a correct PIN.</summary>
    [HttpPost("verify-pin")]
    [ProducesResponseType(typeof(SessionStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<SessionStateResponse> VerifyPin([FromBody] VerifyPinRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Elevation applies to a squad the caller already belongs to. Requiring a session
        // first means the PIN on its own is not enough to reach any squad's data.
        var currentSession = User.GetTeamSession();
        if (currentSession is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "No active squad session",
                Detail = "Join a squad before switching to the Scrum Master role."
            });
        }

        var configuredPin = Environment.GetEnvironmentVariable("SM_PIN")
            ?? _configuration["Auth:ScrumMasterPin"];

        if (string.IsNullOrWhiteSpace(configuredPin))
        {
            _logger.LogError("Scrum Master PIN is not configured. Set the SM_PIN environment variable.");

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Scrum Master access unavailable",
                Detail = "PIN authentication is not configured on this server."
            });
        }

        if (!IsPinCorrect(request.Pin!, configuredPin))
        {
            _logger.LogWarning(
                "Failed PIN elevation for team {TeamId} from {ClientIp}",
                currentSession.Value.TeamId,
                HttpContext.Connection.RemoteIpAddress);

            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Incorrect PIN",
                Detail = "Scrum Master access denied."
            });
        }

        var elevatedSession = currentSession.Value with { Role = RoleType.ScrumMaster };
        IssueSessionCookie(elevatedSession);

        _logger.LogInformation("Session elevated to Scrum Master for team {TeamId}", elevatedSession.TeamId);

        return Ok(new SessionStateResponse(
            true,
            elevatedSession.TeamId,
            null,
            elevatedSession.Role.ToString(),
            elevatedSession.DisplayName));
    }

    /// <summary>Reports the caller's current session so the UI can render the right controls.</summary>
    /// <remarks>
    /// Anonymous so the SPA can ask "who am I?" on first load and get a definite answer
    /// rather than a 401 it has to interpret.
    /// </remarks>
    [AllowAnonymous]
    [HttpGet("session")]
    [ProducesResponseType(typeof(SessionStateResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SessionStateResponse>> GetSession(CancellationToken cancellationToken = default)
    {
        var session = User.GetTeamSession();
        if (session is null)
        {
            return Ok(new SessionStateResponse(false, null, null, RoleType.Developer.ToString(), null));
        }

        var teamName = await _dbContext.Teams
            .AsNoTracking()
            .Where(team => team.Id == session.Value.TeamId)
            .Select(team => team.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new SessionStateResponse(
            true,
            session.Value.TeamId,
            teamName,
            session.Value.Role.ToString(),
            session.Value.DisplayName));
    }

    /// <summary>Clears the session cookie.</summary>
    /// <remarks>Anonymous so that signing out of an already expired session still succeeds.</remarks>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(
            SessionCookie.ResolveName(Request.IsHttps),
            SessionCookie.BuildOptions(Request.IsHttps, expiresAt: null));

        return NoContent();
    }

    /// <summary>Compares the supplied PIN against the configured one in constant time.</summary>
    /// <remarks>
    /// A plain string comparison returns as soon as two characters differ, so response timing
    /// leaks how many leading characters were right and the PIN can be recovered one digit at
    /// a time. Hashing both sides to a fixed width before comparing also stops the length of
    /// the correct PIN leaking through the comparison.
    /// </remarks>
    private static bool IsPinCorrect(string suppliedPin, string configuredPin)
    {
        Span<byte> suppliedHash = stackalloc byte[SHA256.HashSizeInBytes];
        Span<byte> expectedHash = stackalloc byte[SHA256.HashSizeInBytes];

        SHA256.HashData(Encoding.UTF8.GetBytes(suppliedPin.Trim()), suppliedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(configuredPin.Trim()), expectedHash);

        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }

    private void IssueSessionCookie(TeamSession session)
    {
        var token = _tokenIssuer.IssueToken(session, SessionLifetime);
        var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);

        Response.Cookies.Append(
            SessionCookie.ResolveName(Request.IsHttps),
            token,
            SessionCookie.BuildOptions(Request.IsHttps, expiresAt));
    }
}
