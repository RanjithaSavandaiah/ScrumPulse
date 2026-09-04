namespace ScrumPulse.Application.Common.Interfaces;

/// <summary>
/// Scoped tenant accessor resolving the current active team for the HTTP request or execution context.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Current team identifier if scoped to a specific team; null for system/cross-team queries.
    /// </summary>
    Guid? CurrentTeamId { get; set; }
}
