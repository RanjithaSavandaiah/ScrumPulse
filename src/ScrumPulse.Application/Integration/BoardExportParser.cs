namespace ScrumPulse.Application.Integration;

using System.Globalization;
using System.Text;
using ScrumPulse.Domain.Enums;

/// <param name="Warnings">
/// Rows that produced nothing, and why. Returned rather than thrown, so one malformed row out
/// of three hundred neither costs the import nor disappears.
/// </param>
public sealed record BoardExportParseResult(
    IReadOnlyList<ImportedWorkItem> Items,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string>? UnmappedStates = null);

/// <summary>
/// Reads an Azure DevOps query export (CSV) into the same shape the live sync produces, for
/// squads whose client will not issue a token. A CSV carries no revision history, so stage
/// timestamps stay null and the metrics built on them report "not measured" rather than a guess
/// indistinguishable from a real one.
/// </summary>
public static class BoardExportParser
{
    /// <summary>
    /// The parsed result is held in memory before it reaches the database, so this sits well
    /// above a squad's backlog and well below what would exhaust a free-tier container.
    /// </summary>
    public const int MaximumRowCount = 5_000;

    /// <summary>
    /// Candidate header names per field, in order of preference. Several each, because the
    /// export reflects whatever the query carried: "ID", "Work Item Id", or "ID " once Excel
    /// has been involved.
    /// </summary>
    private static readonly string[] IdHeaders = ["id", "work item id", "workitemid", "workitem id"];
    private static readonly string[] TitleHeaders = ["title", "work item title", "name"];
    private static readonly string[] TypeHeaders = ["work item type", "type", "workitemtype"];
    private static readonly string[] StateHeaders = ["state", "status", "board column", "column"];
    private static readonly string[] AssignedToHeaders = ["assigned to", "assignedto", "assignee"];
    private static readonly string[] IterationHeaders = ["iteration path", "iteration", "sprint"];
    private static readonly string[] StoryPointHeaders = ["story points", "storypoints", "effort", "size"];
    private static readonly string[] PriorityHeaders = ["priority"];
    private static readonly string[] CreatedDateHeaders = ["created date", "createddate", "created"];
    private static readonly string[] ClosedDateHeaders = ["closed date", "closeddate", "resolved date"];
    private static readonly string[] ActivatedDateHeaders = ["activated date", "activateddate", "start date"];

    /// <param name="contentPolicy">
    /// Applied here as well as in the merger, so a client's text is dropped at the earliest
    /// point rather than travelling further in.
    /// </param>
    public static BoardExportParseResult Parse(
        string csvContent,
        BoardStateMapping mapping,
        ContentSensitivityPolicy contentPolicy)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new BoardExportParseResult([], ["The file was empty."]);
        }

        var rows = ReadCsvRows(csvContent);

        if (rows.Count <= 1)
        {
            return new BoardExportParseResult([], ["The file contained no rows."]);
        }

        var headerPositions = MapHeaderPositions(rows[0]);
        var idColumn = FindColumn(headerPositions, IdHeaders);

        if (idColumn is null)
        {
            return new BoardExportParseResult(
                [],
                [
                    "No work item ID column was found. Re-export the query with the ID column " +
                    "included, which is what lets a repeated import update the same items " +
                    "instead of duplicating them."
                ]);
        }

        var titleColumn = FindColumn(headerPositions, TitleHeaders);
        var typeColumn = FindColumn(headerPositions, TypeHeaders);
        var stateColumn = FindColumn(headerPositions, StateHeaders);
        var assignedToColumn = FindColumn(headerPositions, AssignedToHeaders);
        var iterationColumn = FindColumn(headerPositions, IterationHeaders);
        var storyPointsColumn = FindColumn(headerPositions, StoryPointHeaders);
        var priorityColumn = FindColumn(headerPositions, PriorityHeaders);
        var createdColumn = FindColumn(headerPositions, CreatedDateHeaders);
        var closedColumn = FindColumn(headerPositions, ClosedDateHeaders);
        var activatedColumn = FindColumn(headerPositions, ActivatedDateHeaders);

        var items = new List<ImportedWorkItem>(rows.Count - 1);
        var warnings = new List<string>();
        var observedStates = new List<string>();
        var seenExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (stateColumn is null)
        {
            warnings.Add(
                "No State column was found, so every item was imported as Backlog. Re-export " +
                "with State included to get stage figures.");
        }

        for (var rowIndex = 1; rowIndex < rows.Count; rowIndex++)
        {
            if (items.Count >= MaximumRowCount)
            {
                warnings.Add(
                    $"The file held more than {MaximumRowCount:N0} rows; the rest were ignored. " +
                    "Narrow the query and import again.");
                break;
            }

            var row = rows[rowIndex];
            var externalId = ReadCell(row, idColumn.Value);

            if (string.IsNullOrWhiteSpace(externalId))
            {
                // Excel leaves a trailing blank line. Only report rows that held something else.
                if (row.Any(cell => !string.IsNullOrWhiteSpace(cell)))
                {
                    warnings.Add($"Row {rowIndex + 1} had no work item ID and was skipped.");
                }

                continue;
            }

            // A query joining parents to children repeats the parent on every child row.
            // Importing it once per child would multiply its story points across the sprint.
            if (!seenExternalIds.Add(externalId))
            {
                continue;
            }

            var boardState = stateColumn is null ? null : ReadCell(row, stateColumn.Value);

            if (!string.IsNullOrWhiteSpace(boardState))
            {
                observedStates.Add(boardState);
            }

            var status = mapping.Resolve(boardState) ?? WorkItemStatus.Backlog;
            var title = titleColumn is null ? null : ReadCell(row, titleColumn.Value);

            items.Add(new ImportedWorkItem
            {
                ExternalId = externalId,
                Key = $"AB#{externalId}",

                Title = contentPolicy == ContentSensitivityPolicy.MetricsOnly
                    ? $"AB#{externalId}"
                    : SensitiveContentRedactor.Redact(title) ?? $"AB#{externalId}",

                // Never read, whatever the policy: the likeliest column to carry stack traces
                // and customer names, and no delivery figure depends on it.
                Description = string.Empty,

                // Constructing one would publish the client's organisation and project names.
                ExternalUrl = null,

                Type = MapWorkItemType(typeColumn is null ? null : ReadCell(row, typeColumn.Value)),
                Status = status,
                Priority = MapPriority(priorityColumn is null ? null : ReadCell(row, priorityColumn.Value)),
                StoryPoints = ParseStoryPoints(storyPointsColumn is null ? null : ReadCell(row, storyPointsColumn.Value)),
                AssignedToDisplayName = NormaliseIdentity(
                    assignedToColumn is null ? null : ReadCell(row, assignedToColumn.Value)),
                IterationPath = iterationColumn is null ? null : ReadCell(row, iterationColumn.Value),
                CreatedAtUtc = ParseDate(createdColumn is null ? null : ReadCell(row, createdColumn.Value))
                    ?? DateTime.UtcNow,
                BoardState = boardState,

                // The only two an export can support, and only when those columns were included.
                Timeline = new StageTimeline
                {
                    PickedUpAtUtc = ParseDate(activatedColumn is null ? null : ReadCell(row, activatedColumn.Value)),
                    CompletedAtUtc = ParseDate(closedColumn is null ? null : ReadCell(row, closedColumn.Value))
                }
            });
        }

        var unmappedStates = mapping.FindUnmappedStates(observedStates);

        if (unmappedStates.Count > 0)
        {
            warnings.Add(
                "These board states had no mapping and were imported as Backlog: " +
                string.Join(", ", unmappedStates) +
                ". Map them on the connection settings page and import again.");
        }

        if (items.Count > 0)
        {
            warnings.Add(
                "A CSV export carries no history, so stage timestamps could not be derived. " +
                "Cycle-time figures for these items will read as not measured until the board " +
                "is connected with a token.");
        }

        return new BoardExportParseResult(items, warnings, [.. unmappedStates]);
    }

    /// <summary>
    /// Splits CSV into rows and cells, honouring quoted fields. Hand-written because RFC 4180
    /// fully specifies the small grammar needed and a parsing dependency would run over
    /// untrusted uploads. Quoting is what matters: titles hold commas, descriptions hold
    /// newlines, and a naive split shifts columns by one into data that still looks plausible.
    /// </summary>
    private static List<string[]> ReadCsvRows(string content)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var isInsideQuotes = false;

        // A BOM survives Excel's round trip and would glue itself to the first header, leaving
        // the ID column permanently unmatched.
        var text = content.TrimStart('﻿');

        for (var position = 0; position < text.Length; position++)
        {
            var character = text[position];

            if (isInsideQuotes)
            {
                if (character != '"')
                {
                    currentCell.Append(character);
                    continue;
                }

                // A doubled quote inside a quoted field is an escaped quote, not the end of it.
                if (position + 1 < text.Length && text[position + 1] == '"')
                {
                    currentCell.Append('"');
                    position++;
                    continue;
                }

                isInsideQuotes = false;
                continue;
            }

            switch (character)
            {
                case '"':
                    isInsideQuotes = true;
                    break;

                case ',':
                    currentRow.Add(currentCell.ToString().Trim());
                    currentCell.Clear();
                    break;

                case '\r':
                    // Swallowed; the line ends on the \n that follows.
                    break;

                case '\n':
                    currentRow.Add(currentCell.ToString().Trim());
                    currentCell.Clear();
                    rows.Add([.. currentRow]);
                    currentRow.Clear();
                    break;

                default:
                    currentCell.Append(character);
                    break;
            }
        }

        // The last row usually has no trailing newline.
        if (currentCell.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentCell.ToString().Trim());
            rows.Add([.. currentRow]);
        }

        return rows;
    }

    private static Dictionary<string, int> MapHeaderPositions(string[] headerRow)
    {
        var positions = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var column = 0; column < headerRow.Length; column++)
        {
            var header = headerRow[column].Trim();

            if (header.Length == 0)
            {
                continue;
            }

            // First occurrence wins. A query with the same column twice is unusual, but
            // overwriting would silently point at whichever came last.
            positions.TryAdd(header, column);
        }

        return positions;
    }

    private static int? FindColumn(Dictionary<string, int> headerPositions, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (headerPositions.TryGetValue(candidate, out var exactPosition))
            {
                return exactPosition;
            }
        }

        // Contains-match fallback for the qualified names some templates emit, such as
        // "Microsoft.VSTS.Scheduling.StoryPoints". Ordered by position first, because
        // dictionary enumeration order is not guaranteed and two runs over the same file could
        // otherwise pick different columns. Leftmost wins: arbitrary, but stable.
        foreach (var candidate in candidates)
        {
            var match = headerPositions
                .Where(entry => entry.Key.Replace(".", " ").Contains(candidate, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.Value)
                .Select(entry => (int?)entry.Value)
                .FirstOrDefault();

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string ReadCell(string[] row, int column) =>
        column >= 0 && column < row.Length ? row[column].Trim() : string.Empty;

    /// <summary>
    /// Reduces an exported <c>Display Name &lt;email@domain&gt;</c> to the name. Roster matching
    /// needs only that, and a client domain's address list is data this deployment should not hold.
    /// </summary>
    private static string? NormaliseIdentity(string? exportedIdentity)
    {
        if (string.IsNullOrWhiteSpace(exportedIdentity))
        {
            return null;
        }

        var emailStart = exportedIdentity.IndexOf('<', StringComparison.Ordinal);
        var displayName = emailStart > 0 ? exportedIdentity[..emailStart] : exportedIdentity;

        return displayName.Trim().Length == 0 ? null : displayName.Trim();
    }

    private static WorkItemType MapWorkItemType(string? exportedType) =>
        exportedType?.Trim().ToLowerInvariant() switch
        {
            "bug" or "defect" or "issue" => WorkItemType.Bug,
            "task" => WorkItemType.TaskPbi,
            "epic" or "feature" => WorkItemType.UserStory,
            "user story" or "product backlog item" or "requirement" or "story" => WorkItemType.UserStory,
            _ => WorkItemType.UserStory
        };

    /// <summary>
    /// Azure DevOps priority runs 1-as-most-urgent, inverted against ScrumPulse's scale. Assume
    /// rather than convert and every item shows as low priority.
    /// </summary>
    private static PriorityLevel MapPriority(string? exportedPriority) =>
        int.TryParse(exportedPriority, NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority)
            ? priority switch
            {
                1 => PriorityLevel.Critical,
                2 => PriorityLevel.High,
                3 => PriorityLevel.Medium,
                _ => PriorityLevel.Low
            }
            : PriorityLevel.Medium;

    /// <summary>
    /// Reads story points, which export as a decimal even when whole. A blank cell is zero, not
    /// a default estimate, which would inflate velocity.
    /// </summary>
    private static int ParseStoryPoints(string? exportedPoints) =>
        double.TryParse(exportedPoints, NumberStyles.Float, CultureInfo.InvariantCulture, out var points)
            ? (int)Math.Round(Math.Clamp(points, 0, 1_000))
            : 0;

    /// <summary>
    /// Invariant culture first, then the server's, because the export follows the locale of
    /// whoever downloaded it. Everything is stored UTC; a mixture of kinds in one column gives
    /// cycle times wrong by the offset that merely look odd.
    /// </summary>
    private static DateTime? ParseDate(string? exportedDate)
    {
        if (string.IsNullOrWhiteSpace(exportedDate))
        {
            return null;
        }

        const DateTimeStyles styles = DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal;

        if (DateTime.TryParse(exportedDate, CultureInfo.InvariantCulture, styles, out var invariantDate))
        {
            return invariantDate;
        }

        return DateTime.TryParse(exportedDate, CultureInfo.CurrentCulture, styles, out var localeDate)
            ? localeDate
            : null;
    }
}
