namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Where a work item came from.
/// </summary>
/// <remarks>
/// Determines whether the item may be edited in ScrumPulse. Anything imported from a delivery
/// board is read-only here: the board remains the system of record, and letting both sides
/// diverge would make the cycle-time figures impossible to defend when a delivery lead asks
/// where a number came from.
/// </remarks>
public enum WorkItemOrigin
{
    /// <summary>Created and maintained in ScrumPulse. Fully editable.</summary>
    Manual = 0,

    /// <summary>Synced from Azure DevOps. Read-only, refreshed on each sync.</summary>
    AzureDevOps = 1,

    /// <summary>Loaded from an exported file. Read-only until the next import replaces it.</summary>
    FileImport = 2
}
