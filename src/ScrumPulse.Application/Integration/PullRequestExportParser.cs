namespace ScrumPulse.Application.Integration;

using System.Globalization;
using System.Text;

/// <summary>A single row parsed from a PR CSV export.</summary>
public sealed record ParsedPullRequestRow(
    string PrNumber,
    string Title,
    string Author,
    string? Reviewer,
    string? Sprint,
    int TotalCommentsCount,
    int ActionableCommentsCount,
    string ReviewStatus,
    string PrUrl,
    string ReviewSummary,
    DateTime? CreatedAtUtc
);

/// <summary>Result of parsing a PR CSV export.</summary>
public sealed record PullRequestExportParseResult(
    IReadOnlyList<ParsedPullRequestRow> Items,
    IReadOnlyList<string> Warnings
);

/// <summary>
/// Reads a CSV export of pull requests into structured records for review quality metrics.
/// </summary>
public static class PullRequestExportParser
{
    public const int MaximumRowCount = 2_000;

    private static readonly string[] PrNumberHeaders = ["pr number", "pr", "pull request", "id", "number", "pr #", "pullrequestid"];
    private static readonly string[] TitleHeaders = ["title", "pr title", "name", "subject"];
    private static readonly string[] AuthorHeaders = ["author", "created by", "creator", "developer", "author email", "createdby", "assignee"];
    private static readonly string[] ReviewerHeaders = ["reviewer", "reviewed by", "reviewedby"];
    private static readonly string[] SprintHeaders = ["sprint", "iteration", "sprint name", "iteration path"];
    private static readonly string[] TotalCommentsHeaders = ["total comments", "comments", "total comments count", "comment count"];
    private static readonly string[] ActionableCommentsHeaders = ["actionable comments", "actionable", "actionable comments count"];
    private static readonly string[] StatusHeaders = ["status", "review status", "state"];
    private static readonly string[] UrlHeaders = ["url", "pr url", "link", "html url"];
    private static readonly string[] SummaryHeaders = ["summary", "review summary", "notes", "coaching signals"];
    private static readonly string[] DateHeaders = ["created date", "created", "date", "createddate"];

    public static PullRequestExportParseResult Parse(string csvContent)
    {
        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return new PullRequestExportParseResult([], ["The file was empty."]);
        }

        var rows = ReadCsvRows(csvContent);
        if (rows.Count <= 1)
        {
            return new PullRequestExportParseResult([], ["The file contained no data rows."]);
        }

        var headerPositions = MapHeaderPositions(rows[0]);

        var prNumberCol = FindColumn(headerPositions, PrNumberHeaders);
        var titleCol = FindColumn(headerPositions, TitleHeaders);
        var authorCol = FindColumn(headerPositions, AuthorHeaders);
        var reviewerCol = FindColumn(headerPositions, ReviewerHeaders);
        var sprintCol = FindColumn(headerPositions, SprintHeaders);
        var totalCommentsCol = FindColumn(headerPositions, TotalCommentsHeaders);
        var actionableCommentsCol = FindColumn(headerPositions, ActionableCommentsHeaders);
        var statusCol = FindColumn(headerPositions, StatusHeaders);
        var urlCol = FindColumn(headerPositions, UrlHeaders);
        var summaryCol = FindColumn(headerPositions, SummaryHeaders);
        var dateCol = FindColumn(headerPositions, DateHeaders);

        if (authorCol is null && titleCol is null)
        {
            return new PullRequestExportParseResult([], ["Neither an 'Author' nor 'Title' column was found in the CSV header."]);
        }

        var items = new List<ParsedPullRequestRow>(rows.Count - 1);
        var warnings = new List<string>();

        var rowCount = Math.Min(rows.Count, MaximumRowCount + 1);
        if (rows.Count > MaximumRowCount + 1)
        {
            warnings.Add($"File contained {rows.Count - 1} rows; only the first {MaximumRowCount} were processed.");
        }

        for (var i = 1; i < rowCount; i++)
        {
            var row = rows[i];
            if (row.Length == 0 || (row.Length == 1 && string.IsNullOrWhiteSpace(row[0])))
            {
                continue;
            }

            var author = authorCol.HasValue ? ReadCell(row, authorCol.Value) : string.Empty;
            var title = titleCol.HasValue ? ReadCell(row, titleCol.Value) : string.Empty;

            if (string.IsNullOrWhiteSpace(author) && string.IsNullOrWhiteSpace(title))
            {
                warnings.Add($"Row {i + 1} skipped: both author and title were empty.");
                continue;
            }

            var prNumber = prNumberCol.HasValue ? ReadCell(row, prNumberCol.Value) : string.Empty;
            if (string.IsNullOrWhiteSpace(prNumber))
            {
                prNumber = $"#{i}";
            }
            else if (!prNumber.StartsWith('#'))
            {
                prNumber = $"#{prNumber}";
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                title = $"Pull Request {prNumber}";
            }

            var reviewer = reviewerCol.HasValue ? ReadCell(row, reviewerCol.Value) : null;
            var sprint = sprintCol.HasValue ? ReadCell(row, sprintCol.Value) : null;

            var totalComments = 0;
            if (totalCommentsCol.HasValue && int.TryParse(ReadCell(row, totalCommentsCol.Value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedTotal))
            {
                totalComments = Math.Max(0, parsedTotal);
            }

            var actionableComments = 0;
            if (actionableCommentsCol.HasValue && int.TryParse(ReadCell(row, actionableCommentsCol.Value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedActionable))
            {
                actionableComments = Math.Clamp(parsedActionable, 0, Math.Max(0, totalComments));
            }

            var status = statusCol.HasValue ? ReadCell(row, statusCol.Value) : "Approved";
            if (string.IsNullOrWhiteSpace(status))
            {
                status = "Approved";
            }

            var url = urlCol.HasValue ? ReadCell(row, urlCol.Value) : string.Empty;
            var summary = summaryCol.HasValue ? ReadCell(row, summaryCol.Value) : string.Empty;

            DateTime? createdAt = null;
            if (dateCol.HasValue && DateTime.TryParse(ReadCell(row, dateCol.Value), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var parsedDate))
            {
                createdAt = parsedDate;
            }

            items.Add(new ParsedPullRequestRow(
                prNumber,
                title,
                author,
                string.IsNullOrWhiteSpace(reviewer) ? null : reviewer,
                string.IsNullOrWhiteSpace(sprint) ? null : sprint,
                totalComments,
                actionableComments,
                status,
                url,
                summary,
                createdAt
            ));
        }

        return new PullRequestExportParseResult(items, warnings);
    }

    private static List<string[]> ReadCsvRows(string content)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentCell = new StringBuilder();
        var isInsideQuotes = false;
        var text = content.TrimStart('\uFEFF');

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
            if (header.Length > 0)
            {
                positions.TryAdd(header, column);
            }
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
}
