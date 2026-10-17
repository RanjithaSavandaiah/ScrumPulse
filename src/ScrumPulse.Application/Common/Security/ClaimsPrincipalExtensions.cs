namespace ScrumPulse.Application.Common.Security;

using System.Security.Claims;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Reads the ScrumPulse session out of a validated <see cref="ClaimsPrincipal"/>.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the team session carried by the principal, or null when the caller is
    /// unauthenticated or the token is missing the claims that make it usable.
    /// </summary>
    /// <remarks>
    /// Returning null rather than a default session is deliberate: every caller is forced
    /// to decide what an anonymous request means for its endpoint, instead of silently
    /// inheriting a team or a role.
    /// </remarks>
    public static TeamSession? GetTeamSession(this ClaimsPrincipal? principal)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var teamIdClaim = principal.FindFirstValue(ScrumPulseClaimTypes.TeamId);
        if (!Guid.TryParse(teamIdClaim, out var teamId) || teamId == Guid.Empty)
        {
            return null;
        }

        // An unparseable role is treated as the least privileged role rather than rejected,
        // so that a future role rename degrades access instead of locking members out.
        var roleClaim = principal.FindFirstValue(ScrumPulseClaimTypes.TeamRole);
        var role = Enum.TryParse<RoleType>(roleClaim, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : RoleType.Developer;

        var displayName = principal.FindFirstValue(ScrumPulseClaimTypes.DisplayName);

        return new TeamSession(
            teamId,
            role,
            string.IsNullOrWhiteSpace(displayName) ? "Member" : displayName);
    }

    private static string? FindFirstValue(this ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value;
}
