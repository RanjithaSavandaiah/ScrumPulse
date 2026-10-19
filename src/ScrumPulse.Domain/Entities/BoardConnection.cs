namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

/// <summary>One per squad, and read-only: ScrumPulse pulls and never writes back.</summary>
public class BoardConnection : BaseEntity
{
    public WorkItemOrigin Provider { get; set; } = WorkItemOrigin.AzureDevOps;

    /// <summary>For example https://dev.azure.com/contoso.</summary>
    public string OrganizationUrl { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string? TeamOrAreaPath { get; set; }

    // Encrypted at rest, never returned by any endpoint: a credential that can be read back
    // can be exfiltrated through any read vulnerability.
    public string? EncryptedCredential { get; set; }

    // Cannot be inferred: "Code Review" means PrCreated on one board, PrApproved on another.
    public string? StateMappingJson { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }

    public string? LastSyncStatus { get; set; }

    public int LastSyncItemCount { get; set; }

    // Default keeps a shared project from pulling in people who never agreed to be tracked here.
    public BoardAssigneeFilter AssigneeFilter { get; set; } = BoardAssigneeFilter.RosterMembersOnly;

    // Raising this moves client free text into a hosted database, for no analytical gain.
    public ContentSensitivityPolicy ContentPolicy { get; set; } = ContentSensitivityPolicy.MetricsOnly;

    public bool IsEnabled { get; set; } = true;

    /// <summary>True when a credential has been stored, without revealing it.</summary>
    public bool HasStoredCredential => !string.IsNullOrWhiteSpace(EncryptedCredential);
}
