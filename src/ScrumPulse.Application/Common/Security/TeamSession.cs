namespace ScrumPulse.Application.Common.Security;

using ScrumPulse.Domain.Enums;

/// <summary>
/// An authenticated claim to act on a single team, produced only by the server
/// after the caller proved knowledge of a shared secret (join code, or PIN for elevation).
/// </summary>
/// <param name="TeamId">Team the caller may read and write. Never taken from a request header.</param>
/// <param name="Role">Role granted for that team.</param>
/// <param name="DisplayName">Name stamped onto the audit trail for records this session creates.</param>
public readonly record struct TeamSession(Guid TeamId, RoleType Role, string DisplayName)
{
    /// <summary>
    /// Roles permitted to perform privileged actions such as editing sprint dates,
    /// deleting work items, or changing the roster.
    /// </summary>
    private static readonly RoleType[] PrivilegedRoles =
    [
        RoleType.ScrumMaster,
        RoleType.Cdl,
        RoleType.AgileCoach
    ];

    /// <summary>True when this session may perform Scrum Master level actions.</summary>
    public bool IsPrivileged => Array.IndexOf(PrivilegedRoles, Role) >= 0;
}
