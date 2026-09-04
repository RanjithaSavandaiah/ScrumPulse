namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;

/// <summary>
/// Tenant team entity enabling multi-team isolation within an organization.
/// Allows multiple scrum squads to manage independent sprints, backlogs, and metrics.
/// </summary>
public class Team : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
