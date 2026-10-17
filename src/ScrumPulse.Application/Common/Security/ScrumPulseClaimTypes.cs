namespace ScrumPulse.Application.Common.Security;

/// <summary>
/// Claim type names carried inside the ScrumPulse session token.
/// </summary>
/// <remarks>
/// These names are part of the token wire format. Renaming one invalidates every
/// session token already issued to a browser, so treat changes here as breaking.
/// </remarks>
public static class ScrumPulseClaimTypes
{
    /// <summary>Team the session is scoped to. Authoritative source of tenant identity.</summary>
    public const string TeamId = "sp:tid";

    /// <summary>Role granted for the scoped team, as a <see cref="Domain.Enums.RoleType"/> name.</summary>
    public const string TeamRole = "sp:rol";

    /// <summary>Display name stamped onto audit fields for records the session creates.</summary>
    public const string DisplayName = "sp:usr";
}
