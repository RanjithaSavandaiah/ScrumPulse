namespace ScrumPulse.Application.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Manages the squad's board connection and runs both import routes against it. Tenancy is not
/// enforced here: the context's global filter scopes rows to the session's squad first, and a
/// second check would be a second place to get it wrong.
/// </summary>
public sealed class BoardConnectionService(
    IAppDbContext dbContext,
    IBoardSyncClient boardSyncClient,
    ICredentialProtector credentialProtector,
    TimeProvider timeProvider,
    ILogger<BoardConnectionService> logger) : IBoardConnectionService
{
    /// <summary>
    /// Safety margin on the last run. Board revision timestamps and this server's clock differ,
    /// so an exact cutoff would silently drop anything edited mid-sync.
    /// </summary>
    private static readonly TimeSpan IncrementalSyncOverlap = TimeSpan.FromHours(2);

    public async Task<BoardConnectionDto?> GetConnectionAsync(CancellationToken ct = default)
    {
        var connection = await LoadConnectionAsync(ct);

        return connection is null ? null : ToDto(connection);
    }

    public async Task<BoardConnectionDto> SaveConnectionAsync(
        SaveBoardConnectionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var connections = await dbContext.BoardConnections.ToListAsync(ct);
        var connection = connections
            .OrderByDescending(c => !string.IsNullOrEmpty(c.EncryptedCredential))
            .ThenByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .FirstOrDefault();

        var isNewConnection = connection is null;
        connection ??= new BoardConnection();

        // Enforce the one-connection-per-squad invariant by soft-deleting any duplicate or stale rows
        foreach (var extra in connections.Where(c => c.Id != connection.Id))
        {
            extra.EncryptedCredential = null;
            extra.IsEnabled = false;
            extra.IsDeleted = true;
        }

        connection.Provider = WorkItemOrigin.AzureDevOps;
        connection.OrganizationUrl = request.OrganizationUrl.Trim().TrimEnd('/');
        connection.ProjectName = request.ProjectName.Trim();
        connection.TeamOrAreaPath = string.IsNullOrWhiteSpace(request.TeamOrAreaPath)
            ? null
            : request.TeamOrAreaPath.Trim();
        connection.AssigneeFilter = request.AssigneeFilter;
        connection.ContentPolicy = request.ContentPolicy;
        connection.IsEnabled = request.IsEnabled;

        if (request.StateMapping is { Count: > 0 })
        {
            connection.StateMappingJson = BoardStateMapping.ToJson(request.StateMapping);
        }

        // A null token means keep the stored one. Clearing it instead would disconnect the board
        // every time somebody fixed a typo in the project name.
        if (!string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            connection.EncryptedCredential = credentialProtector.Protect(request.PersonalAccessToken.Trim());

            // The old verdict described a different credential.
            connection.LastSyncStatus = null;
        }

        if (isNewConnection)
        {
            dbContext.BoardConnections.Add(connection);
        }

        await dbContext.SaveChangesAsync(ct);

        // No organisation URL: the logs must not become a list of which clients are delivered to.
        logger.LogInformation(
            "Board connection {Action} with assignee filter {AssigneeFilter} and content policy {ContentPolicy}",
            isNewConnection ? "created" : "updated",
            connection.AssigneeFilter,
            connection.ContentPolicy);

        return ToDto(connection);
    }

    public async Task<bool> DeleteConnectionAsync(CancellationToken ct = default)
    {
        var connections = await dbContext.BoardConnections.ToListAsync(ct);

        if (connections.Count == 0)
        {
            return false;
        }

        // Soft delete leaves the rows in the table, so the credentials have to go first.
        foreach (var connection in connections)
        {
            connection.EncryptedCredential = null;
            connection.IsEnabled = false;
            connection.IsDeleted = true;
        }

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation("Board connection removed and its stored credential destroyed");

        return true;
    }

    public async Task<BoardConnectionTestResult> TestConnectionAsync(CancellationToken ct = default)
    {
        var connection = await LoadConnectionAsync(ct);

        if (connection is null)
        {
            return new BoardConnectionTestResult(false, "No board is connected yet.");
        }

        if (!connection.HasStoredCredential)
        {
            return new BoardConnectionTestResult(
                false,
                "No access token is stored. Add one to connect, or use file import instead.");
        }

        var failure = await boardSyncClient.TestConnectionAsync(connection, ct);

        connection.LastSyncStatus = failure ?? "Connection verified";
        await dbContext.SaveChangesAsync(ct);

        return new BoardConnectionTestResult(failure is null, failure);
    }

    public async Task<BoardSyncResultDto> SyncAsync(bool fullResync = false, CancellationToken ct = default)
    {
        var connection = await LoadConnectionAsync(ct)
            ?? throw new InvalidOperationException("No board is connected for this squad.");

        if (!connection.IsEnabled)
        {
            return EmptyResult(["The board connection is turned off. Enable it to sync."]);
        }

        if (!connection.HasStoredCredential)
        {
            return EmptyResult(
                ["No access token is stored. Add one, or import an exported file instead."]);
        }

        var rosterMembers = await LoadRosterAsync(ct);

        // The board filters on identities, so a roster with no emails returns an empty query
        // that looks exactly like a board with no work on it.
        var rosterIdentities = rosterMembers
            .Select(member => member.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (connection.AssigneeFilter == BoardAssigneeFilter.RosterMembersOnly && rosterIdentities.Length == 0)
        {
            return EmptyResult(
                [
                    "No squad member has a board email address recorded, so there was nothing to " +
                    "match against. Add email addresses to the roster, or switch the connection " +
                    "to import everything under the area path."
                ]);
        }

        var changedSince = fullResync
            ? null
            : connection.LastSyncedAtUtc?.Subtract(IncrementalSyncOverlap);

        IReadOnlyList<ImportedWorkItem> importedItems;

        try
        {
            importedItems = await boardSyncClient.FetchWorkItemsAsync(connection, rosterIdentities, changedSince, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The board's message can echo back organisation and identity details, so only the
            // exception type is logged and the stored status is written here.
            connection.LastSyncStatus = "The last sync failed. Check the connection and try again.";
            await dbContext.SaveChangesAsync(ct);

            logger.LogError(
                exception,
                "Board sync failed with {ExceptionType}",
                exception.GetType().Name);

            return EmptyResult([connection.LastSyncStatus]);
        }

        var result = await ApplyImportedItemsAsync(
            importedItems,
            connection,
            WorkItemOrigin.AzureDevOps,
            rosterMembers,
            ct);

        connection.LastSyncedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        connection.LastSyncItemCount = result.ImportedCount;
        connection.LastSyncStatus = $"Imported {result.ImportedCount} item(s)";

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "Board sync imported {ImportedCount} item(s), {CreatedCount} new, {UnmappedStateCount} unmapped state(s)",
            result.ImportedCount,
            result.CreatedCount,
            result.UnmappedStates.Count);

        return result;
    }

    public async Task<BoardSyncResultDto> ImportFromFileAsync(string csvContent, CancellationToken ct = default)
    {
        var connection = await LoadConnectionAsync(ct);

        // A squad with no board access still needs somewhere for its mapping and content policy
        // to live, so the first file import creates a credential-less connection.
        if (connection is null)
        {
            connection = new BoardConnection
            {
                Provider = WorkItemOrigin.FileImport,
                OrganizationUrl = string.Empty,
                ProjectName = string.Empty,
                IsEnabled = true
            };

            dbContext.BoardConnections.Add(connection);
        }

        var mapping = BoardStateMapping.FromJson(connection.StateMappingJson);
        var parseResult = BoardExportParser.Parse(csvContent, mapping, connection.ContentPolicy);

        if (parseResult.Items.Count == 0)
        {
            return EmptyResult(parseResult.Warnings);
        }

        var rosterMembers = await LoadRosterAsync(ct);

        var importable = parseResult.Items;
        var excludedCount = 0;

        if (connection.AssigneeFilter == BoardAssigneeFilter.RosterMembersOnly)
        {
            importable = FilterToRoster(parseResult.Items, rosterMembers, out excludedCount);
        }

        var warnings = new List<string>(parseResult.Warnings);

        if (excludedCount > 0)
        {
            warnings.Add(
                $"{excludedCount} item(s) assigned to people outside this squad were not " +
                "imported, as the connection is set to roster members only.");
        }

        var result = await ApplyImportedItemsAsync(
            importable,
            connection,
            WorkItemOrigin.FileImport,
            rosterMembers,
            ct);

        connection.LastSyncedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        connection.LastSyncItemCount = result.ImportedCount;
        connection.LastSyncStatus = $"Imported {result.ImportedCount} item(s) from a file";

        await dbContext.SaveChangesAsync(ct);

        logger.LogInformation(
            "File import brought in {ImportedCount} item(s), {CreatedCount} new",
            result.ImportedCount,
            result.CreatedCount);

        var combinedUnmapped = new SortedSet<string>(result.UnmappedStates, StringComparer.OrdinalIgnoreCase);
        if (parseResult.UnmappedStates is not null)
        {
            foreach (var unmapped in parseResult.UnmappedStates)
            {
                combinedUnmapped.Add(unmapped);
            }
        }

        return result with
        {
            UnmappedStates = [.. combinedUnmapped],
            Warnings = [.. warnings, .. result.Warnings]
        };
    }

    /// <summary>
    /// Upserts imported items and reports what could not be matched. Shared by both routes,
    /// since nothing past this point depends on whether the items arrived over HTTP or in a file.
    /// </summary>
    private async Task<BoardSyncResultDto> ApplyImportedItemsAsync(
        IReadOnlyList<ImportedWorkItem> importedItems,
        BoardConnection connection,
        WorkItemOrigin origin,
        IReadOnlyList<TeamMember> rosterMembers,
        CancellationToken ct)
    {
        if (importedItems.Count == 0)
        {
            return EmptyResult([]);
        }

        var externalIds = importedItems
            .Select(item => item.ExternalId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // One query for the batch. A few hundred round trips to a free-tier database is the
        // difference between a one-second sync and a timeout.
        var existingItemsList = await dbContext.WorkItems
            .Where(item => item.ExternalId != null && externalIds.Contains(item.ExternalId))
            .ToListAsync(ct);

        var existingItems = existingItemsList
            .GroupBy(item => item.ExternalId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var allSprints = await dbContext.Sprints
            .Select(sprint => new { sprint.Id, sprint.Name })
            .ToListAsync(ct);

        var sprintsByName = allSprints
            .GroupBy(sprint => sprint.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);

        var membersByName = BuildRosterLookup(rosterMembers);

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var createdCount = 0;
        var importedCount = 0;
        var skippedManualCount = 0;

        var unmappedStates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var unmatchedAssignees = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var importedItem in importedItems)
        {
            var isNewItem = !existingItems.TryGetValue(importedItem.ExternalId, out var workItem);

            if (isNewItem)
            {
                workItem = new WorkItem();
                dbContext.WorkItems.Add(workItem);
            }

            var outcome = WorkItemImportMerger.Merge(
                workItem!,
                importedItem,
                origin,
                connection.ContentPolicy,
                isNewItem,
                utcNow);

            if (outcome == WorkItemImportMerger.MergeOutcome.SkippedManualItem)
            {
                skippedManualCount++;
                continue;
            }

            ResolveAssignee(workItem!, importedItem, membersByName, unmatchedAssignees);
            ResolveSprint(workItem!, importedItem, sprintsByName);

            foreach (var unmappedState in importedItem.Timeline.UnmappedStates)
            {
                unmappedStates.Add(unmappedState);
            }

            if (!string.IsNullOrWhiteSpace(importedItem.BoardState) &&
                importedItem.Status == WorkItemStatus.Backlog &&
                origin == WorkItemOrigin.AzureDevOps)
            {
                // Symptom of a missing mapping. Named because every stage figure for that
                // column otherwise reads as a silent zero.
                unmappedStates.Add(importedItem.BoardState);
            }

            importedCount++;

            if (isNewItem)
            {
                createdCount++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        if (skippedManualCount > 0)
        {
            warnings.Add(
                $"{skippedManualCount} item(s) were left untouched because they were created " +
                "here by hand. Delete them if the board should own them instead.");
        }

        return new BoardSyncResultDto(
            importedCount,
            createdCount,
            [.. unmappedStates],
            [.. unmatchedAssignees],
            warnings,
            utcNow);
    }

    /// <summary>
    /// Attaches the item to a roster member, or leaves it imported but unattributed. Guessing
    /// would put somebody else's work on a person's record, which is worse than an obvious gap.
    /// </summary>
    private static void ResolveAssignee(
        WorkItem workItem,
        ImportedWorkItem importedItem,
        IReadOnlyDictionary<string, Guid> membersByName,
        SortedSet<string> unmatchedAssignees)
    {
        if (string.IsNullOrWhiteSpace(importedItem.AssignedToDisplayName))
        {
            workItem.AssigneeId = null;
            return;
        }

        if (membersByName.TryGetValue(importedItem.AssignedToDisplayName.Trim(), out var memberId))
        {
            workItem.AssigneeId = memberId;
            return;
        }

        workItem.AssigneeId = null;
        unmatchedAssignees.Add(importedItem.AssignedToDisplayName.Trim());
    }

    /// <summary>
    /// Matches the board's iteration to a sprint by the leaf of its backslash-separated path.
    /// No match leaves the item outside any sprint; an invented sprint has no dates and would
    /// distort every burndown it appeared in.
    /// </summary>
    private static void ResolveSprint(
        WorkItem workItem,
        ImportedWorkItem importedItem,
        IReadOnlyDictionary<string, Guid> sprintsByName)
    {
        if (string.IsNullOrWhiteSpace(importedItem.IterationPath))
        {
            return;
        }

        var iterationLeaf = importedItem.IterationPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            ?? importedItem.IterationPath;

        if (sprintsByName.TryGetValue(iterationLeaf.Trim(), out var sprintId))
        {
            workItem.SprintId = sprintId;
        }
    }

    /// <summary>
    /// Drops items assigned outside the squad. The live sync filters in the board query itself;
    /// a human-exported file may hold the whole project, so the rule runs again on arrival.
    /// Unassigned items are kept, being usually the squad's own unstarted backlog.
    /// </summary>
    private static IReadOnlyList<ImportedWorkItem> FilterToRoster(
        IReadOnlyList<ImportedWorkItem> items,
        IReadOnlyList<TeamMember> rosterMembers,
        out int excludedCount)
    {
        var rosterNames = BuildRosterLookup(rosterMembers);

        var kept = items
            .Where(item =>
                string.IsNullOrWhiteSpace(item.AssignedToDisplayName) ||
                rosterNames.ContainsKey(item.AssignedToDisplayName.Trim()))
            .ToList();

        excludedCount = items.Count - kept.Count;

        return kept;
    }

    /// <summary>
    /// Indexes the roster by name, email and the local part of the address. One key is not
    /// enough ("Priya Raman" on the board, "priya.raman@contoso.com" in the roster), and fuzzy
    /// matching would eventually attribute one person's work to another.
    /// </summary>
    private static Dictionary<string, Guid> BuildRosterLookup(IReadOnlyList<TeamMember> rosterMembers)
    {
        var lookup = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in rosterMembers)
        {
            if (!string.IsNullOrWhiteSpace(member.Name))
            {
                lookup.TryAdd(member.Name.Trim(), member.Id);
            }

            if (string.IsNullOrWhiteSpace(member.Email))
            {
                continue;
            }

            lookup.TryAdd(member.Email.Trim(), member.Id);

            var localPart = member.Email.Split('@')[0].Replace('.', ' ').Trim();

            if (localPart.Length > 0)
            {
                lookup.TryAdd(localPart, member.Id);
            }
        }

        return lookup;
    }

    // Ordered descending so connections with stored credentials and latest saved settings are always returned.
    private Task<BoardConnection?> LoadConnectionAsync(CancellationToken ct) =>
        dbContext.BoardConnections
            .OrderByDescending(connection => !string.IsNullOrEmpty(connection.EncryptedCredential))
            .ThenByDescending(connection => connection.UpdatedAtUtc ?? connection.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    private Task<List<TeamMember>> LoadRosterAsync(CancellationToken ct) =>
        dbContext.TeamMembers
            .Where(member => member.IsActive)
            .ToListAsync(ct);

    private BoardSyncResultDto EmptyResult(IReadOnlyList<string> warnings) =>
        new(0, 0, [], [], warnings, timeProvider.GetUtcNow().UtcDateTime);

    private static BoardConnectionDto ToDto(BoardConnection connection) =>
        new(
            connection.Id,
            connection.Provider,
            connection.OrganizationUrl,
            connection.ProjectName,
            connection.TeamOrAreaPath,
            connection.HasStoredCredential,
            connection.AssigneeFilter,
            connection.ContentPolicy,
            connection.IsEnabled,
            connection.LastSyncedAtUtc,
            connection.LastSyncStatus,
            connection.LastSyncItemCount,
            BoardStateMapping.FromJson(connection.StateMappingJson).ToDictionary());
}
