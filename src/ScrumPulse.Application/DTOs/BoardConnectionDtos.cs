namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

// The credential is absent in every form, token, mask and length alike. HasStoredCredential is
// all the UI needs.
public sealed record BoardConnectionDto(
    Guid Id,
    WorkItemOrigin Provider,
    string OrganizationUrl,
    string ProjectName,
    string? TeamOrAreaPath,
    bool HasStoredCredential,
    BoardAssigneeFilter AssigneeFilter,
    ContentSensitivityPolicy ContentPolicy,
    bool IsEnabled,
    DateTime? LastSyncedAtUtc,
    string? LastSyncStatus,
    int LastSyncItemCount,
    IReadOnlyDictionary<string, WorkItemStatus> StateMapping);

/// <param name="PersonalAccessToken">
/// Sent once, encrypted immediately, never returned. Null on a later save means keep the stored
/// token, so correcting the project name does not silently disconnect the board.
/// </param>
/// <param name="StateMapping">Empty falls back to the stock Agile, Scrum and CMMI defaults.</param>
public sealed record SaveBoardConnectionRequest(
    [Required]
    [Url]
    [StringLength(300, MinimumLength = 8)]
    string OrganizationUrl,

    [Required]
    [StringLength(200, MinimumLength = 1)]
    string ProjectName,

    [StringLength(400)]
    string? TeamOrAreaPath,

    [StringLength(500, MinimumLength = 8)]
    string? PersonalAccessToken,

    BoardAssigneeFilter AssigneeFilter = BoardAssigneeFilter.RosterMembersOnly,

    ContentSensitivityPolicy ContentPolicy = ContentSensitivityPolicy.MetricsOnly,

    bool IsEnabled = true,

    IReadOnlyDictionary<string, WorkItemStatus>? StateMapping = null);

/// <param name="UnmappedStates">
/// Reported rather than defaulted: a guessed column distorts every figure derived from it with
/// nothing on screen to explain why.
/// </param>
public sealed record BoardSyncResultDto(
    int ImportedCount,
    int CreatedCount,
    IReadOnlyList<string> UnmappedStates,
    IReadOnlyList<string> UnmatchedAssignees,
    IReadOnlyList<string> Warnings,
    DateTime SyncedAtUtc);

// Message is never the board's raw response, which echoes organisation and identity details.
public sealed record BoardConnectionTestResult(bool Succeeded, string? Message);

public sealed record BoardStateMappingDefaultsDto(
    IReadOnlyDictionary<string, WorkItemStatus> AzureDevOpsDefaults);
