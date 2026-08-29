namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Lifecycle status of a technical debt item.
/// </summary>
public enum TechDebtStatus
{
    Identified = 0,
    Planned = 1,
    InProgress = 2,
    Resolved = 3,
    Deferred = 4
}
