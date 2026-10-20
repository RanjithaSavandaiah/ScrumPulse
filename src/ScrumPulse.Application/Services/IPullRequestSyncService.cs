namespace ScrumPulse.Application.Services;

using ScrumPulse.Application.DTOs;

/// <summary>
/// Synchronises pull request review logs and comment analytics from Azure DevOps or CSV file imports.
/// </summary>
public interface IPullRequestSyncService
{
    /// <summary>
    /// Fetches PRs from the configured Azure DevOps board connection, analyses review comments,
    /// and updates developer PR review logs.
    /// </summary>
    Task<PullRequestSyncResultDto> SyncFromAzureDevOpsAsync(CancellationToken ct = default);

    /// <summary>
    /// Parses an exported CSV file and idempotently syncs developer PR logs.
    /// </summary>
    Task<PullRequestSyncResultDto> ImportFromCsvAsync(string csvContent, CancellationToken ct = default);
}
