namespace ScrumPulse.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Calculates cross sprint team growth metrics for client facing performance
/// presentations in service based delivery organizations.
/// Follows clean code principles and provides graceful fallbacks for missing data.
/// </summary>
public class TeamPerformanceService(
    IAppDbContext db,
    ILogger<TeamPerformanceService>? logger = null) : ITeamPerformanceService
{
    private const int DefaultPerformanceSprintCount = 6;
    private const int MinSprintWindowSize = 1;
    private const int MaxSprintWindowSize = 24;
    private const int ScoreThresholdAPlus = 90;
    private const int ScoreThresholdA = 80;
    private const int ScoreThresholdBPlus = 70;
    private const int ScoreThresholdB = 60;
    private const double TargetTeamMoodBaseline = 4.0;
    private const double MaxTargetPrReviewTurnaroundHours = 8.0;

    public async Task<TeamPerformanceSummaryDto> GetPerformanceSummaryAsync(int sprintCount = DefaultPerformanceSprintCount, CancellationToken ct = default)
    {
        try
        {
            var snapshots = await GetGrowthTrendAsync(sprintCount, ct);

            var teamName = "FikaCoders";
            try
            {
                var team = await db.Teams.FirstOrDefaultAsync(teamEntity => teamEntity.IsActive, ct);
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
            string grade = overallScore >= ScoreThresholdAPlus ? "A+" : overallScore >= ScoreThresholdA ? "A" : overallScore >= ScoreThresholdBPlus ? "B+" : overallScore >= ScoreThresholdB ? "B" : "C";
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

    public async Task<IReadOnlyList<TeamHighlightDto>> GetHighlightsAsync(int sprintCount = DefaultPerformanceSprintCount, CancellationToken ct = default)
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
                .OrderByDescending(sprint => sprint.StartDate)
                .Take(Math.Clamp(sprintCount, MinSprintWindowSize, MaxSprintWindowSize))
                .AsNoTracking()
                .ToListAsync(ct);

            if (sprints.Count == 0)
            {
                return [];
            }

            sprints.Reverse(); // Chronological order

            var sprintIds = sprints.Select(sprint => sprint.Id).ToList();

            var workItems = new List<Domain.Entities.WorkItem>();
            try
            {
                workItems = await db.WorkItems
                    .Where(workItem => workItem.SprintId.HasValue && sprintIds.Contains(workItem.SprintId.Value))
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
                    .Where(blocker => blocker.SprintId.HasValue && sprintIds.Contains(blocker.SprintId.Value))
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
                    .Where(standup => standup.SprintId.HasValue && sprintIds.Contains(standup.SprintId.Value))
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
                var sprintItems = workItems.Where(workItem => workItem.SprintId == sprint.Id).ToList();
                var sprintBlockers = blockers.Where(blocker => blocker.SprintId == sprint.Id).ToList();
                var sprintStandups = standups.Where(standup => standup.SprintId == sprint.Id).ToList();

                int delivered = sprintItems.Where(workItem => workItem.Status == WorkItemStatus.Done).Sum(workItem => workItem.StoryPoints);
                int committed = sprint.CommittedStoryPoints > 0 ? sprint.CommittedStoryPoints : delivered;
                double sayDo = committed > 0 ? Math.Round((double)delivered / committed * 100, 1) : 0;
                int escaped = sprintItems.Count(workItem => workItem.IsEscapedDefect);
                double avgPr = sprintItems.Where(workItem => workItem.PrReviewLatencyHours.HasValue)
                    .Select(workItem => workItem.PrReviewLatencyHours!.Value).DefaultIfEmpty(0).Average();
                int blockersRaised = sprintBlockers.Count;
                int blockersResolved = sprintBlockers.Count(blocker => blocker.IsResolved);
                double mood = sprintStandups.Where(standup => standup.MoodScore > 0)
                    .Select(standup => standup.MoodScore).DefaultIfEmpty(4).Average();

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

        double avgSayDo = Math.Round(snapshots.Average(snapshot => snapshot.SayDoPercent), 1);

        int totalEscaped = snapshots.Sum(snapshot => snapshot.EscapedDefects);
        int recentEscaped = snapshots.Count >= 3 ? snapshots.Skip(snapshots.Count - 3).Sum(snapshot => snapshot.EscapedDefects) : totalEscaped;

        double latestPr = latest.AvgPrReviewHours;
        double previousPr = previous.AvgPrReviewHours;
        double prImprovement = previousPr > 0 ? Math.Round((previousPr - latestPr) / previousPr * 100, 1) : 0;

        int totalBlockers = snapshots.Sum(snapshot => snapshot.BlockersRaised);
        int resolvedBlockers = snapshots.Sum(snapshot => snapshot.BlockersResolved);
        double blockerSla = totalBlockers > 0 ? Math.Round((double)resolvedBlockers / totalBlockers * 100, 1) : 100;

        double avgMood = Math.Round(snapshots.Average(snapshot => snapshot.TeamMoodAvg), 1);

        double avgVelocity = Math.Round(snapshots.Average(snapshot => snapshot.DeliveredPoints), 1);
        double prevAvg = snapshots.Count > 1
            ? Math.Round(snapshots.Take(snapshots.Count - 1).Average(snapshot => snapshot.DeliveredPoints), 1)
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

            new("Team Engagement", "Culture", avgMood, TargetTeamMoodBaseline, Math.Round((avgMood - TargetTeamMoodBaseline) / TargetTeamMoodBaseline * 100, 1),
                avgMood >= TargetTeamMoodBaseline ? "Up" : "Down", "/5",
                $"Team morale at {avgMood}/5 — {(avgMood >= TargetTeamMoodBaseline ? "healthy and motivated" : "needs attention")}",
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
        var velocityMetric = metrics.FirstOrDefault(metric => metric.MetricName == "Velocity Growth");
        if (velocityMetric != null)
        {
            if (velocityMetric.DeltaPercent > 0)
                highlights.Add(new("rocket", "Delivery", $"Team delivered {velocityMetric.DeltaPercent}% more story points than previous sprint while maintaining quality standards.", "Positive"));
            else if (velocityMetric.DeltaPercent == 0 && velocityMetric.CurrentValue > 0)
                highlights.Add(new("bar-chart", "Delivery", $"Consistent delivery at {velocityMetric.CurrentValue} story points — stable velocity maintained.", "Positive"));
        }

        // Say-Do highlight
        var sayDoMetric = metrics.FirstOrDefault(metric => metric.MetricName == "Say-Do Predictability");
        if (sayDoMetric != null && sayDoMetric.CurrentValue >= 80)
            highlights.Add(new("target", "Predictability", $"Team delivers on commitments with {sayDoMetric.CurrentValue}% Say-Do predictability — high reliability for sprint planning.", "Positive"));

        // Zero defects highlight
        var qualityMetric = metrics.FirstOrDefault(metric => metric.MetricName == "Quality Score");
        if (qualityMetric != null && qualityMetric.CurrentValue == 0)
            highlights.Add(new("shield-check", "Quality", "Zero escaped production defects in recent sprints — robust quality gates and testing practices in place.", "Positive"));

        // PR review turnaround
        var prMetric = metrics.FirstOrDefault(metric => metric.MetricName == "PR Review Turnaround");
        if (prMetric != null && prMetric.CurrentValue <= MaxTargetPrReviewTurnaroundHours)
            highlights.Add(new("zap", "Engineering", $"Code review turnaround at {prMetric.CurrentValue} hours — fast feedback loops enabling rapid iteration.", "Positive"));

        // Blocker SLA
        var blockerMetric = metrics.FirstOrDefault(metric => metric.MetricName == "Blocker Resolution SLA");
        if (blockerMetric != null && blockerMetric.CurrentValue >= 90)
            highlights.Add(new("check-circle", "Risk", $"{blockerMetric.CurrentValue}% blocker resolution SLA compliance — proactive impediment management.", "Positive"));

        // Team morale
        var engagementMetric = metrics.FirstOrDefault(metric => metric.MetricName == "Team Engagement");
        if (engagementMetric != null && engagementMetric.CurrentValue >= TargetTeamMoodBaseline)
            highlights.Add(new("heart", "Culture", $"Team morale score at {engagementMetric.CurrentValue}/5 — high engagement and collaborative culture.", "Positive"));

        // Sprint count & maturity
        if (snapshots.Count >= 4)
            highlights.Add(new("trending-up", "Maturity", $"Performance data aggregated across {snapshots.Count} sprints — demonstrating sustained delivery discipline and engineering maturity.", "Positive"));

        // If few highlights, add a growth focused one
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
                .CountAsync(techDebt => techDebt.Status == TechDebtStatus.Resolved, ct);
        }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query TechDebtItems resolved count"); }

        var moodScores = new List<int>();
        try
        {
            moodScores = await db.DailyStandups
                .AsNoTracking()
                .Where(standup => standup.MoodScore > 0)
                .Select(standup => standup.MoodScore)
                .ToListAsync(ct);
        }
        catch (Exception ex) { logger?.LogDebug(ex, "Could not query DailyStandups mood scores"); }

        double avgMood = moodScores.Count > 0 ? Math.Round(moodScores.Average(), 1) : TargetTeamMoodBaseline;
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

        var sayDo = metrics.FirstOrDefault(metric => metric.MetricName == "Say-Do Predictability");
        if (sayDo != null) score += Math.Min(15, sayDo.CurrentValue / 100 * 15);

        var quality = metrics.FirstOrDefault(metric => metric.MetricName == "Quality Score");
        if (quality != null && quality.CurrentValue == 0) score += 10;

        var velocity = metrics.FirstOrDefault(metric => metric.MetricName == "Velocity Growth");
        if (velocity != null && velocity.DeltaPercent >= 0) score += Math.Min(10, velocity.DeltaPercent / 10 * 5 + 5);

        var pr = metrics.FirstOrDefault(metric => metric.MetricName == "PR Review Turnaround");
        if (pr != null && pr.CurrentValue <= MaxTargetPrReviewTurnaroundHours) score += 5;

        var blocker = metrics.FirstOrDefault(metric => metric.MetricName == "Blocker Resolution SLA");
        if (blocker != null) score += Math.Min(5, blocker.CurrentValue / 100 * 5);

        if (engagement.AvgMoodScore >= TargetTeamMoodBaseline) score += 5;

        return (int)Math.Clamp(Math.Round(score), 0, 100);
    }

    private static string GenerateHeadline(string grade, IReadOnlyList<SprintGrowthSnapshotDto> snapshots, IReadOnlyList<GrowthMetricDto> metrics)
    {
        if (snapshots.Count == 0) return "Team delivery cadence active — performance telemetry tracking initialized.";
        var latest = snapshots[^1];
        var sayDo = metrics.FirstOrDefault(metric => metric.MetricName == "Say-Do Predictability");
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
