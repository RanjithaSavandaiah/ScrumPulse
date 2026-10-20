namespace ScrumPulse.Application.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Implements live Azure DevOps PR synchronization and CSV file import for developer PR analytics.
/// </summary>
public sealed class PullRequestSyncService(
    IAppDbContext db,
    IBoardSyncClient boardSyncClient,
    TimeProvider timeProvider,
    ILogger<PullRequestSyncService> logger) : IPullRequestSyncService
{
    public async Task<PullRequestSyncResultDto> SyncFromAzureDevOpsAsync(CancellationToken ct = default)
    {
        var connection = await db.BoardConnections
            .OrderByDescending(c => !string.IsNullOrEmpty(c.EncryptedCredential))
            .ThenByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No board connection is configured for this squad.");

        if (!connection.IsEnabled)
        {
            return new PullRequestSyncResultDto(0, 0, 0, timeProvider.GetUtcNow().UtcDateTime, [],
                ["The board connection is turned off. Enable it to sync."]);
        }

        if (!connection.HasStoredCredential)
        {
            return new PullRequestSyncResultDto(0, 0, 0, timeProvider.GetUtcNow().UtcDateTime, [],
                ["No access token is stored. Configure a personal access token in board settings."]);
        }

        var rosterMembers = await db.TeamMembers.AsNoTracking().ToListAsync(ct);
        var rosterIdentities = rosterMembers
            .Select(m => m.Email)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sprints = await db.Sprints.AsNoTracking().ToListAsync(ct);

        IReadOnlyList<ImportedPullRequest> importedPrs;
        try
        {
            importedPrs = await boardSyncClient.FetchPullRequestsAsync(connection, rosterIdentities, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to fetch pull requests from Azure DevOps.");
            return new PullRequestSyncResultDto(0, 0, 0, timeProvider.GetUtcNow().UtcDateTime, [],
                ["Could not reach Azure DevOps Git service or read pull requests. Check your credentials and permissions."]);
        }

        var createdCount = 0;
        var updatedCount = 0;
        var unmatchedAuthors = new List<string>();
        var warnings = new List<string>();

        foreach (var pr in importedPrs)
        {
            var author = FindMember(pr.AuthorIdentity, pr.AuthorDisplayName, rosterMembers);
            if (author is null)
            {
                var label = !string.IsNullOrWhiteSpace(pr.AuthorDisplayName)
                    ? pr.AuthorDisplayName
                    : (pr.AuthorIdentity ?? "Unknown Developer");
                unmatchedAuthors.Add(label);
                continue;
            }

            var reviewer = FindMember(pr.ReviewerIdentity, pr.ReviewerDisplayName, rosterMembers);
            var prDate = pr.ClosedAtUtc ?? pr.CreatedAtUtc;
            var sprint = sprints.FirstOrDefault(s => s.StartDate <= prDate && prDate <= s.EndDate.AddDays(1))
                ?? sprints.FirstOrDefault(s => s.IsActive);

            var reviewStatus = MapStatus(pr.Status);

            var existing = await db.PullRequestReviewLogs
                .FirstOrDefaultAsync(l => l.PrNumber == pr.PrNumber || (!string.IsNullOrEmpty(pr.Url) && l.PrUrl == pr.Url), ct);

            if (existing != null)
            {
                existing.PrTitle = pr.Title;
                existing.TotalCommentsCount = pr.TotalCommentsCount;
                existing.ActionableCommentsCount = pr.ActionableCommentsCount;
                existing.ReviewSummary = pr.ReviewSummary;
                existing.ReviewStatus = reviewStatus;
                if (reviewer != null) existing.ReviewerId = reviewer.Id;
                if (sprint != null && !existing.SprintId.HasValue) existing.SprintId = sprint.Id;
                if (reviewStatus == ReviewStatusType.Merged)
                {
                    existing.MergedAtUtc = pr.ClosedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime;
                }
                updatedCount++;
            }
            else
            {
                var newLog = new PullRequestReviewLog
                {
                    AuthorId = author.Id,
                    ReviewerId = reviewer?.Id,
                    SprintId = sprint?.Id,
                    PrNumber = pr.PrNumber,
                    PrTitle = pr.Title,
                    PrUrl = pr.Url,
                    TotalCommentsCount = pr.TotalCommentsCount,
                    ActionableCommentsCount = pr.ActionableCommentsCount,
                    ReviewSummary = pr.ReviewSummary,
                    ReviewStatus = reviewStatus,
                    MergedAtUtc = reviewStatus == ReviewStatusType.Merged
                        ? (pr.ClosedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime)
                        : null
                };
                db.PullRequestReviewLogs.Add(newLog);
                createdCount++;
            }
        }

        if (unmatchedAuthors.Count > 0)
        {
            warnings.Add($"{unmatchedAuthors.Distinct().Count()} author(s) not found on squad roster were skipped.");
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Azure DevOps PR sync complete: {Imported} total, {Created} created, {Updated} updated",
            createdCount + updatedCount,
            createdCount,
            updatedCount);

        return new PullRequestSyncResultDto(
            createdCount + updatedCount,
            createdCount,
            updatedCount,
            timeProvider.GetUtcNow().UtcDateTime,
            unmatchedAuthors.Distinct().ToList(),
            warnings);
    }

    public async Task<PullRequestSyncResultDto> ImportFromCsvAsync(string csvContent, CancellationToken ct = default)
    {
        var parseResult = PullRequestExportParser.Parse(csvContent);
        if (parseResult.Items.Count == 0)
        {
            return new PullRequestSyncResultDto(0, 0, 0, timeProvider.GetUtcNow().UtcDateTime, [], parseResult.Warnings.ToList());
        }

        var rosterMembers = await db.TeamMembers.AsNoTracking().ToListAsync(ct);
        var sprints = await db.Sprints.AsNoTracking().ToListAsync(ct);

        var createdCount = 0;
        var updatedCount = 0;
        var unmatchedAuthors = new List<string>();
        var warnings = new List<string>(parseResult.Warnings);

        foreach (var item in parseResult.Items)
        {
            var author = FindMember(item.Author, item.Author, rosterMembers);
            if (author is null)
            {
                unmatchedAuthors.Add(item.Author);
                continue;
            }

            var reviewer = !string.IsNullOrWhiteSpace(item.Reviewer)
                ? FindMember(item.Reviewer, item.Reviewer, rosterMembers)
                : null;

            Sprint? sprint = null;
            if (!string.IsNullOrWhiteSpace(item.Sprint))
            {
                sprint = sprints.FirstOrDefault(s => s.Name.Equals(item.Sprint, StringComparison.OrdinalIgnoreCase));
            }
            if (sprint == null && item.CreatedAtUtc.HasValue)
            {
                sprint = sprints.FirstOrDefault(s => s.StartDate <= item.CreatedAtUtc && item.CreatedAtUtc <= s.EndDate.AddDays(1));
            }
            sprint ??= sprints.FirstOrDefault(s => s.IsActive);

            var reviewStatus = MapStatus(item.ReviewStatus);

            var existing = await db.PullRequestReviewLogs
                .FirstOrDefaultAsync(l => l.PrNumber == item.PrNumber || (!string.IsNullOrEmpty(item.PrUrl) && l.PrUrl == item.PrUrl), ct);

            if (existing != null)
            {
                existing.PrTitle = item.Title;
                existing.TotalCommentsCount = item.TotalCommentsCount;
                existing.ActionableCommentsCount = item.ActionableCommentsCount;
                existing.ReviewSummary = item.ReviewSummary;
                existing.ReviewStatus = reviewStatus;
                if (reviewer != null) existing.ReviewerId = reviewer.Id;
                if (sprint != null && !existing.SprintId.HasValue) existing.SprintId = sprint.Id;
                if (reviewStatus == ReviewStatusType.Merged && !existing.MergedAtUtc.HasValue)
                {
                    existing.MergedAtUtc = item.CreatedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime;
                }
                updatedCount++;
            }
            else
            {
                var newLog = new PullRequestReviewLog
                {
                    AuthorId = author.Id,
                    ReviewerId = reviewer?.Id,
                    SprintId = sprint?.Id,
                    PrNumber = item.PrNumber,
                    PrTitle = item.Title,
                    PrUrl = item.PrUrl,
                    TotalCommentsCount = item.TotalCommentsCount,
                    ActionableCommentsCount = item.ActionableCommentsCount,
                    ReviewSummary = item.ReviewSummary,
                    ReviewStatus = reviewStatus,
                    MergedAtUtc = reviewStatus == ReviewStatusType.Merged
                        ? (item.CreatedAtUtc ?? timeProvider.GetUtcNow().UtcDateTime)
                        : null
                };
                db.PullRequestReviewLogs.Add(newLog);
                createdCount++;
            }
        }

        if (unmatchedAuthors.Count > 0)
        {
            warnings.Add($"{unmatchedAuthors.Distinct().Count()} author(s) not found on squad roster were skipped.");
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Pull request CSV import complete: {Imported} total, {Created} created, {Updated} updated",
            createdCount + updatedCount,
            createdCount,
            updatedCount);

        return new PullRequestSyncResultDto(
            createdCount + updatedCount,
            createdCount,
            updatedCount,
            timeProvider.GetUtcNow().UtcDateTime,
            unmatchedAuthors.Distinct().ToList(),
            warnings);
    }

    private static TeamMember? FindMember(string? identity, string? displayName, IReadOnlyList<TeamMember> rosterMembers)
    {
        if (string.IsNullOrWhiteSpace(identity) && string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        return rosterMembers.FirstOrDefault(m =>
            (!string.IsNullOrWhiteSpace(identity) && !string.IsNullOrWhiteSpace(m.Email) && m.Email.Equals(identity, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(displayName) && !string.IsNullOrWhiteSpace(m.Email) && m.Email.Equals(displayName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(displayName) && m.Name.Equals(displayName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(identity) && m.Name.Equals(identity, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(displayName) && m.Name.Contains(displayName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(displayName) && displayName.Contains(m.Name, StringComparison.OrdinalIgnoreCase)));
    }

    private static ReviewStatusType MapStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ReviewStatusType.Approved;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "completed" or "merged" => ReviewStatusType.Merged,
            "abandoned" or "closed" => ReviewStatusType.Closed,
            "changesrequested" or "changes requested" or "rejected" => ReviewStatusType.ChangesRequested,
            _ => ReviewStatusType.Approved
        };
    }
}
