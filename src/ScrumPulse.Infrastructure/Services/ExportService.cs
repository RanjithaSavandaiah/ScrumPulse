namespace ScrumPulse.Infrastructure.Services;

using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Services;

public class ExportService(IAppDbContext db) : IExportService
{
    public async Task<ExportFileResult?> ExportSprintCsvAsync(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await db.Sprints
            .Include(sprintEntity => sprintEntity.WorkItems)
                .ThenInclude(workItemEntity => workItemEntity.Assignee)
            .FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == sprintId, ct);

        if (sprint == null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("Key,Title,Type,Status,Priority,StoryPoints,Assignee,PickedUpAtUtc,PrCreatedAtUtc,PrApprovedAtUtc,PrMergedAtUtc,QaStartedAtUtc,CompletedAtUtc,DevCycleHours,PrReviewLatencyHours,TotalCycleHours,IsEscapedDefect,DaysInStatus");

        foreach (var item in sprint.WorkItems.OrderBy(workItem => workItem.Key))
        {
            var cleanTitle = SanitizeCsvField(item.Title);
            var cleanAssignee = SanitizeCsvField(item.Assignee?.Name ?? "Unassigned");
            var pickedUp = item.PickedUpAtUtc?.ToString("o") ?? "";
            var prCreated = item.PrCreatedAtUtc?.ToString("o") ?? "";
            var prApproved = item.PrApprovedAtUtc?.ToString("o") ?? "";
            var prMerged = item.PrMergedAtUtc?.ToString("o") ?? "";
            var qaStarted = item.QaStartedAtUtc?.ToString("o") ?? "";
            var completed = item.CompletedAtUtc?.ToString("o") ?? "";
            sb.AppendLine($"\"{SanitizeCsvField(item.Key)}\",\"{cleanTitle}\",{item.Type},{item.Status},{item.Priority},{item.StoryPoints},\"{cleanAssignee}\",\"{pickedUp}\",\"{prCreated}\",\"{prApproved}\",\"{prMerged}\",\"{qaStarted}\",\"{completed}\",{item.DevCycleTimeHours ?? 0},{item.PrReviewLatencyHours ?? 0},{item.TotalCycleTimeHours ?? 0},{item.IsEscapedDefect},{item.DaysInCurrentStatus}");
        }

        // Use MemoryStream instead of LINQ Concat+ToArray for efficient byte output
        using var ms = new MemoryStream();
        ms.Write(Encoding.UTF8.GetPreamble());
        ms.Write(Encoding.UTF8.GetBytes(sb.ToString()));
        var bytes = ms.ToArray();

        var cleanSprintName = Regex.Replace(sprint.Name, @"[^a-zA-Z0-9_\-]", "_");

        return new ExportFileResult(bytes, "text/csv", $"{cleanSprintName}_Report_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    public async Task<ExportFileResult> ExportEnterpriseJsonAsync(CancellationToken ct = default)
    {
        var sprints = await db.Sprints.Include(sprint => sprint.WorkItems).AsNoTracking().ToListAsync(ct);
        var members = await db.TeamMembers.AsNoTracking().ToListAsync(ct);
        var blockers = await db.Blockers.AsNoTracking().ToListAsync(ct);
        var feedbacks = await db.Monthly1on1Feedbacks.AsNoTracking().ToListAsync(ct);
        var kudos = await db.KudosCards.AsNoTracking().ToListAsync(ct);
        var leaves = await db.TeamLeaves.AsNoTracking().ToListAsync(ct);
        var standups = await db.DailyStandups.AsNoTracking().ToListAsync(ct);

        var bundle = new
        {
            ExportedAtUtc = DateTime.UtcNow,
            Platform = "ScrumPulse Enterprise",
            Sprints = sprints,
            TeamMembers = members,
            Blockers = blockers,
            MonthlyFeedbacks = feedbacks,
            Kudos = kudos,
            Leaves = leaves,
            DailyStandups = standups
        };

        // Use stream-based serialization to reduce peak memory usage
        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, bundle, new JsonSerializerOptions { WriteIndented = true }, ct);
        var bytes = ms.ToArray();

        return new ExportFileResult(bytes, "application/json", $"ScrumPulse_Export_{DateTime.UtcNow:yyyyMMdd}.json");
    }

    /// <summary>
    /// Sanitizes a string for CSV output to prevent formula injection attacks.
    /// Prefixes cells starting with formula trigger characters (=, +, -, @, \t, \r)
    /// with a single-quote to neutralize them when opened in Excel/Google Sheets.
    /// Also escapes embedded double-quotes per RFC 4180.
    /// </summary>
    private static string SanitizeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // Escape embedded double-quotes (RFC 4180)
        var escaped = value.Replace("\"", "\"\"");

        // Neutralize formula injection: prefix with single-quote if cell starts with trigger char
        if (escaped.Length > 0 && escaped[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            escaped = "'" + escaped;
        }

        return escaped;
    }
}

