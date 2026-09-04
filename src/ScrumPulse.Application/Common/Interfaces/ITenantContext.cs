namespace ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Scoped tenant and user context resolving the active team and operator identity for the HTTP request.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Current team identifier if scoped to a specific team; null for system/cross-team queries.
    /// </summary>
    Guid? CurrentTeamId { get; set; }

    /// <summary>
    /// Current authenticated or acting user name / role for audit trail stamping.
    /// </summary>
    string? CurrentUser { get; set; }
}
