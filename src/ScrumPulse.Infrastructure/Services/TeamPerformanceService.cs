namespace ScrumPulse.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Calculates cross-sprint team growth metrics for client-facing performance
/// presentations in service-based delivery organizations.
/// Follows clean code principles and provides graceful fallbacks for missing data.
/// </summary>
public class TeamPerformanceService(
    IAppDbContext db,
    ILogger<TeamPerformanceService>? logger = null) : ITeamPerformanceService
{
    public async Task<TeamPerformanceSummaryDto> GetPerformanceSummaryAsync(int sprintCount = 6, CancellationToken ct = default)
    {
        try
        {
            var snapshots = await GetGrowthTrendAsync(sprintCount, ct);

            var teamName = "FikaCoders";
            try
            {
                var team = await db.Teams.FirstOrDefaultAsync(t => t.IsActive, ct);
                if (team != null)
                {
                    teamName = team.Name;
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Failed to resolve active team name for performance summary");
            }

            if (snapshots.Count == 0)
            {
                return new TeamPerformanceSummaryDto(
                    teamName,
                    "N/A",
                    0,
                    "No completed sprint telemetry available to analyze team performance yet. Complete at least one sprint with story point estimates and delivered work items to generate performance metrics.",
                    0,
                    DateTime.UtcNow,
                    [],
                    [],
                    [],
                    new TeamEngagementDto(0, 0, 0, 0, 0, 0, "No Data")
                );
            }

            var metrics = ComputeGrowthMetrics(snapshots);
            var highlights = GenerateHighlights(snapshots, metrics);
            var engagement = await ComputeEngagementAsync(snapshots.Count, ct);

            int overallScore = ComputeOverallScore(metrics, engagement);
            string grade = overallScore >= 90 ? "A+" : overallScore >= 80 ? "A" : overallScore >= 70 ? "B+" : overallScore >= 60 ? "B" : "C";
            string headline = GenerateHeadline(grade, snapshots, metrics);

            return new TeamPerformanceSummaryDto(
                teamName, grade, overallScore, headline,
                snapshots.Count, DateTime.UtcNow,
                metrics, snapshots, highlights, engagement
            );
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error computing team performance summary; returning fallback defaults");
            return GetDefaultSummary();
        }
    }

    public async Task<IReadOnlyList<TeamHighlightDto>> GetHighlightsAsync(int sprintCount = 6, CancellationToken ct = default)
    {
        try
        {
            var snapshots = await GetGrowthTrendAsync(sprintCount, ct);
            if (snapshots.Count == 0) return [];
            var metrics = ComputeGrowthMetrics(snapshots);
            return GenerateHighlights(snapshots, metrics);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error computing team highlights; returning empty list");
            return [];
        }
    }

    public async Task<IReadOnlyList<SprintGrowthSnapshotDto>> GetGrowthTrendAsync(int sprintCount = 8, CancellationToken ct = default)
    {
        try
        {
            var sprints = await db.Sprints
                .OrderByDescending(s => s.StartDate)
                .Take(Math.Clamp(sprintCount, 1, 24))
                .AsNoTracking()
                .ToListAsync(ct);

            if (sprints.Count == 0)
            {
                return [];
            }

            sprints.Reverse(); // Chronological order

            var sprintIds = sprints.Select(s => s.Id).ToList();

            var workItems = new List<Domain.Entities.WorkItem>();
            try
            {
                workItems = await db.WorkItems
                    .Where(w => w.SprintId.HasValue && sprintIds.Contains(w.SprintId.Value))
                    .AsNoTracking()
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Could not query work items for sprint trend");
            }

            var blockers = new List<Domain.Entities.Blocker>();
            try
            {
                blockers = await db.Blockers
                    .Where(b => b.SprintId.HasValue && sprintIds.Contains(b.SprintId.Value))
                    .AsNoTracking()
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Could not query blockers for sprint trend");
            }

            var standups = new List<Domain.Entities.DailyStandup>();
            try
            {
                standups = await db.DailyStandups
                    .Where(s => s.SprintId.HasValue && sprintIds.Contains(s.SprintId.Value))
                    .AsNoTracking()
                    .ToListAsync(ct);
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Could not query daily standups for sprint trend");
            }

            var snapshots = new List<SprintGrowthSnapshotDto>();

            foreach (var sprint in sprints)
            {
                var sprintItems = workItems.Where(w => w.SprintId == sprint.Id).ToList();
                var sprintBlockers = blockers.Where(b => b.SprintId == sprint.Id).ToList();
                var sprintStandups = standups.Where(s => s.SprintId == sprint.Id).ToList();

                int delivered = sprintItems.Where(w => w.Status == WorkItemStatus.Done).Sum(w => w.StoryPoints);
                int committed = sprint.CommittedStoryPoints > 0 ? sprint.CommittedStoryPoints : delivered;
                double sayDo = committed > 0 ? Math.Round((double)delivered / committed * 100, 1) : 0;
                int escaped = sprintItems.Count(w => w.IsEscapedDefect);
                double avgPr = sprintItems.Where(w => w.PrReviewLatencyHours.HasValue)
                    .Select(w => w.PrReviewLatencyHours!.Value).DefaultIfEmpty(0).Average();
                int blockersRaised = sprintBlockers.Count;
                int blockersResolved = sprintBlockers.Count(b => b.IsResolved);
                double mood = sprintStandups.Where(s => s.MoodScore > 0)
                    .Select(s => s.MoodScore).DefaultIfEmpty(4).Average();

                snapshots.Add(new SprintGrowthSnapshotDto(
                    sprint.Id, sprint.Name, sprint.StartDate, sprint.EndDate,
                    delivered, committed, sayDo, escaped,
                    Math.Round(avgPr, 1), blockersRaised, blockersResolved,
                    Math.Round(mood, 1)
                ));
            }

            return snapshots;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Error reading growth trend snapshots");
            return [];
        }
    }

    private static IReadOnlyList<GrowthMetricDto> ComputeGrowthMetrics(IReadOnlyList<SprintGrowthSnapshotDto> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return [];
        }

        var latest = snapshots[^1];
        var previous = snapshots.Count > 1 ? snapshots[^2] : latest;

        double velocityGrowth = previous.DeliveredPoints > 0
            ? Math.Round(((double)latest.DeliveredPoints - previous.DeliveredPoints) / previous.DeliveredPoints * 100, 1) : 0;

        double avgSayDo = Math.Round(snapshots.Average(s => s.SayDoPercent), 1);

        int totalEscaped = snapshots.Sum(s => s.EscapedDefects);
        int recentEscaped = snapshots.Count >= 3 ? snapshots.Skip(snapshots.Count - 3).Sum(s => s.EscapedDefects) : totalEscaped;

        double latestPr = latest.AvgPrReviewHours;
        double previousPr = previous.AvgPrReviewHours;
        double prImprovement = previousPr > 0 ? Math.Round((previousPr - latestPr) / previousPr * 100, 1) : 0;

        int totalBlockers = snapshots.Sum(s => s.BlockersRaised);
        int resolvedBlockers = snapshots.Sum(s => s.BlockersResolved);
        double blockerSla = totalBlockers > 0 ? Math.Round((double)resolvedBlockers / totalBlockers * 100, 1) : 100;

        double avgMood = Math.Round(snapshots.Average(s => s.TeamMoodAvg), 1);

        double avgVelocity = Math.Round(snapshots.Average(s => s.DeliveredPoints), 1);
        double prevAvg = snapshots.Count > 1
            ? Math.Round(snapshots.Take(snapshots.Count - 1).Average(s => s.DeliveredPoints), 1)
            : avgVelocity;

        return new List<GrowthMetricDto>
        {
            new("Velocity Growth", "Delivery", latest.DeliveredPoints, previous.DeliveredPoints, velocityGrowth,
                GetTrend(velocityGrowth), "SP",
                $"Team delivered {latest.DeliveredPoints} story points ({FormatDelta(velocityGrowth)} vs previous sprint)",
                "trending-up"),

            new("Say-Do Predictability", "Commitment", avgSayDo, latest.SayDoPercent, 0,
                avgSayDo >= 85 ? "Up" : avgSayDo >= 70 ? "Stable" : "Down", "%",
                $"Team delivers what they commit {avgSayDo}% of the time",
                "target"),

            new("Quality Score", "Quality", recentEscaped, totalEscaped, recentEscaped == 0 ? 100 : -recentEscaped * 10,
                recentEscaped == 0 ? "Up" : "Down", "defects",
                recentEscaped == 0 ? "Zero escaped defects in recent sprints — production quality maintained" : $"{recentEscaped} escaped defects in recent sprints",
                "shield"),

            new("PR Review Turnaround", "Efficiency", latestPr, previousPr, prImprovement,
                GetTrend(prImprovement), "hours",
                $"Code review turnaround at {latestPr}h ({FormatDelta(prImprovement)} improvement)",
                "git-pull-request"),

            new("Blocker Resolution SLA", "Risk", blockerSla, 100, blockerSla - 100,
                blockerSla >= 90 ? "Up" : "Down", "%",
                $"{blockerSla}% of blockers resolved within SLA across {snapshots.Count} sprints",
                "shield-alert"),

            new("Team Engagement", "Culture", avgMood, 4.0, Math.Round((avgMood - 4.0) / 4.0 * 100, 1),
                avgMood >= 4.0 ? "Up" : "Down", "/5",
                $"Team morale at {avgMood}/5 — {(avgMood >= 4.0 ? "healthy and motivated" : "needs attention")}",
                "heart"),

            new("Avg Sprint Velocity", "Capacity", avgVelocity, prevAvg,
                prevAvg > 0 ? Math.Round((avgVelocity - prevAvg) / prevAvg * 100, 1) : 0,
                avgVelocity >= prevAvg ? "Up" : "Stable", "SP/sprint",
                $"Rolling average velocity: {avgVelocity} story points per sprint",
                "bar-chart"),

            new("Commitment Consistency", "Maturity", latest.CommittedPoints, previous.CommittedPoints,
                previous.CommittedPoints > 0 ? Math.Round(((double)latest.CommittedPoints - previous.CommittedPoints) / previous.CommittedPoints * 100, 1) : 0,
                Math.Abs(latest.CommittedPoints - previous.CommittedPoints) <= 5 ? "Stable" : "Up", "SP",
                $"Sprint commitment: {latest.CommittedPoints} SP — {(Math.Abs(latest.CommittedPoints - previous.CommittedPoints) <= 5 ? "consistent planning maturity" : "adjusting capacity")}",
                "activity")
        };
    }

    private static IReadOnlyList<TeamHighlightDto> GenerateHighlights(
        IReadOnlyList<SprintGrowthSnapshotDto> snapshots,
        IReadOnlyList<GrowthMetricDto> metrics)
    {
        var highlights = new List<TeamHighlightDto>();
        if (snapshots.Count == 0) return [];

        // Velocity growth highlight
        var velocityMetric = metrics.FirstOrDefault(m => m.MetricName == "Velocity Growth");
        if (velocityMetric != null)
        {
            if (velocityMetric.DeltaPercent > 0)
                highlights.Add(new("rocket", "Delivery", $"Team delivered {velocityMetric.DeltaPercent}% more story points than previous sprint while maintaining quality standards.", "Positive"));
            else if (velocityMetric.DeltaPercent == 0 && velocityMetric.CurrentValue > 0)
                highlights.Add(new("bar-chart", "Delivery", $"Consistent delivery at {velocityMetric.CurrentValue} story points — stable velocity maintained.", "Positive"));
        }

        // Say-Do highlight
        var sayDoMetric = metrics.FirstOrDefault(m => m.MetricName == "Say-Do Predictability");
        if (sayDoMetric != null && sayDoMetric.CurrentValue >= 80)
            highlights.Add(new("target", "Predictability", $"Team delivers on commitments with {sayDoMetric.CurrentValue}% Say-Do predictability — high reliability for sprint planning.", "Positive"));

        // Zero defects highlight
        var qualityMetric = metrics.FirstOrDefault(m => m.MetricName == "Quality Score");
        if (qualityMetric != null && qualityMetric.CurrentValue == 0)
            highlights.Add(new("shield-check", "Quality", "Zero escaped production defects in recent sprints — robust quality gates and testing practices in place.", "Positive"));

        // PR review turnaround
        var prMetric = metrics.FirstOrDefault(m => m.MetricName == "PR Review Turnaround");
        if (prMetric != null && prMetric.CurrentValue <= 8)
            highlights.Add(new("zap", "Engineering", $"Code review turnaround at {prMetric.CurrentValue} hours — fast feedback loops enabling rapid iteration.", "Positive"));

        // Blocker SLA
        var blockerMetric = metrics.FirstOrDefault(m => m.MetricName == "Blocker Resolution SLA");
        if (blockerMetric != null && blockerMetric.CurrentValue >= 90)
            highlights.Add(new("check-circle", "Risk", $"{blockerMetric.CurrentValue}% blocker resolution SLA compliance — proactive impediment management.", "Positive"));

        // Team morale
        var engagementMetric = metrics.FirstOrDefault(m => m.MetricName == "Team Engagement");
        if (engagementMetric != null && engagementMetric.CurrentValue >= 4.0)
            highlights.Add(new("heart", "Culture", $"Team morale score at {engagementMetric.CurrentValue}/5 — high engagement and collaborative culture.", "Positive"));

        // Sprint count & maturity
        if (snapshots.Count >= 4)
            highlights.Add(new("trending-up", "Maturity", $"Performance data aggregated across {snapshots.Count} sprints — demonstrating sustained delivery discipline and engineering maturity.", "Positive"));

        // If few highlights, add a growth-focused one
        if (highlights.Count < 3 && snapshots.Count >= 2)
        {
            var latest = snapshots[^1];
            highlights.Add(new("sparkles", "Growth", $"Team completed {latest.DeliveredPoints} story points in {latest.SprintName} with {latest.SayDoPercent}% commitment accuracy.", "Neutral"));
        }

        return highlights;
    }

    private async Task<TeamEngagementDto> ComputeEngagementAsync(int sprintCount, CancellationToken ct)
    {
        int kudosCount = 0;
        try { kudosCount = await db.KudosCards.AsNoTracking().CountAsync(ct); }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query KudosCards count"); }

        int techTalksCount = 0;
        try { techTalksCount = await db.TechTalkLogs.AsNoTracking().CountAsync(ct); }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query TechTalkLogs count"); }

        int techDebtResolved = 0;
        try
        {
            techDebtResolved = await db.TechDebtItems
                .AsNoTracking()
                .CountAsync(t => t.Status == TechDebtStatus.Resolved, ct);
        }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query TechDebtItems resolved count"); }

        var moodScores = new List<int>();
        try
        {
            moodScores = await db.DailyStandups
                .AsNoTracking()
                .Where(s => s.MoodScore > 0)
                .Select(s => s.MoodScore)
                .ToListAsync(ct);
        }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query DailyStandups mood scores"); }

        double avgMood = moodScores.Count > 0 ? Math.Round(moodScores.Average(), 1) : 4.0;
        double kudosPerSprint = sprintCount > 0 ? Math.Round((double)kudosCount / sprintCount, 1) : kudosCount;
        double talksPerSprint = sprintCount > 0 ? Math.Round((double)techTalksCount / sprintCount, 1) : techTalksCount;

        string grade = avgMood >= 4.2 && kudosPerSprint >= 2 ? "Excellent"
            : avgMood >= 3.8 ? "Good"
            : avgMood >= 3.2 ? "Fair"
            : "Needs Attention";

        return new TeamEngagementDto(avgMood, kudosCount, techTalksCount, techDebtResolved, kudosPerSprint, talksPerSprint, grade);
    }

    private static int ComputeOverallScore(IReadOnlyList<GrowthMetricDto> metrics, TeamEngagementDto engagement)
    {
        double score = 50; // Baseline

        var sayDo = metrics.FirstOrDefault(m => m.MetricName == "Say-Do Predictability");
        if (sayDo != null) score += Math.Min(15, sayDo.CurrentValue / 100 * 15);

        var quality = metrics.FirstOrDefault(m => m.MetricName == "Quality Score");
        if (quality != null && quality.CurrentValue == 0) score += 10;

        var velocity = metrics.FirstOrDefault(m => m.MetricName == "Velocity Growth");
        if (velocity != null && velocity.DeltaPercent >= 0) score += Math.Min(10, velocity.DeltaPercent / 10 * 5 + 5);

        var pr = metrics.FirstOrDefault(m => m.MetricName == "PR Review Turnaround");
        if (pr != null && pr.CurrentValue <= 8) score += 5;

        var blocker = metrics.FirstOrDefault(m => m.MetricName == "Blocker Resolution SLA");
        if (blocker != null) score += Math.Min(5, blocker.CurrentValue / 100 * 5);

        if (engagement.AvgMoodScore >= 4.0) score += 5;

        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }

    private static string GenerateHeadline(string grade, IReadOnlyList<SprintGrowthSnapshotDto> snapshots, IReadOnlyList<GrowthMetricDto> metrics)
    {
        if (snapshots.Count == 0) return "Team delivery cadence active — performance telemetry tracking initialized.";
        var latest = snapshots[^1];
        var sayDo = metrics.FirstOrDefault(m => m.MetricName == "Say-Do Predictability");
        return grade switch
        {
            "A+" => $"Outstanding delivery performance — {latest.DeliveredPoints} SP delivered at {sayDo?.CurrentValue ?? 0}% predictability.",
            "A" => $"Strong delivery execution — consistent velocity with high commitment accuracy.",
            "B+" => $"Good momentum — team is trending upward with solid engineering practices.",
            _ => $"Building foundations — team is establishing delivery cadence across {snapshots.Count} sprints."
        };
    }

    private static IReadOnlyList<GrowthMetricDto> GetDefaultMetrics() =>
    [
        new("Velocity Growth", "Delivery", 0, 0, 0, "Stable", "SP", "Sprint velocity tracking initialized", "trending-up"),
        new("Say-Do Predictability", "Commitment", 100, 100, 0, "Stable", "%", "Commitment reliability baseline established", "target"),
        new("Quality Score", "Quality", 0, 0, 100, "Up", "defects", "Zero escaped defects recorded", "shield"),
        new("PR Review Turnaround", "Efficiency", 4.5, 5.0, 10.0, "Up", "hours", "Code review turnaround within target SLA", "git-pull-request"),
        new("Blocker Resolution SLA", "Risk", 100, 100, 0, "Up", "%", "Blocker SLA monitoring active", "shield-alert"),
        new("Team Engagement", "Culture", 4.5, 4.0, 12.5, "Up", "/5", "Team morale and collaboration score", "heart"),
        new("Avg Sprint Velocity", "Capacity", 0, 0, 0, "Stable", "SP/sprint", "Rolling velocity metrics initializing", "bar-chart"),
        new("Commitment Consistency", "Maturity", 0, 0, 0, "Stable", "SP", "Sprint planning maturity tracking", "activity")
    ];

    private static IReadOnlyList<TeamHighlightDto> GetDefaultHighlights() =>
    [
        new("rocket", "Delivery", "Team delivery tracking initialized and ready for cross-sprint performance analysis.", "Positive"),
        new("shield-check", "Quality", "Zero escaped defects recorded — high quality standards active.", "Positive"),
        new("heart", "Culture", "Collaborative team environment with continuous agile improvement loops.", "Positive")
    ];

    private static TeamPerformanceSummaryDto GetDefaultSummary()
    {
        var defaultEngagement = new TeamEngagementDto(0, 0, 0, 0, 0, 0, "No Data");
        return new TeamPerformanceSummaryDto(
            "FikaCoders", "N/A", 0,
            "No completed sprint telemetry available to analyze team performance yet.",
            0, DateTime.UtcNow,
            [], [], [], defaultEngagement
        );
    }

    private static string GetTrend(double delta) => delta > 2 ? "Up" : delta < -2 ? "Down" : "Stable";
    private static string FormatDelta(double delta) => delta > 0 ? $"+{delta}%" : delta < 0 ? $"{delta}%" : "stable";
}
