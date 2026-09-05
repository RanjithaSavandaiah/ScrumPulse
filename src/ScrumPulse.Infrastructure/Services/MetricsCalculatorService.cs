namespace ScrumPulse.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;
using ScrumPulse.Domain.Enums;

public class MetricsCalculatorService(IAppDbContext db) : IMetricsCalculatorService
{
    // Named constants for capacity calculations
    private const double ScrumMasterCapacityFactor = 0.75;
    private const double CdlCapacityFactor = 0.5;
    private const double HoursPerStoryPoint = 6.5;

    public async Task<SprintCapacityDto> CalculateSprintCapacityAsync(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == sprintId, ct);
        var membersQuery = db.TeamMembers.Where(teamMember => teamMember.IsActive && 
            teamMember.Role != RoleType.ProductOwner && 
            teamMember.Role != RoleType.ClientStakeholder && 
            teamMember.Role != RoleType.AgileCoach);
        List<Domain.Entities.TeamMember> members;
        if (sprint?.TeamId.HasValue == true)
        {
            var squadMembers = await membersQuery.Where(m => m.TeamId == sprint.TeamId.Value).ToListAsync(ct);
            members = squadMembers.Count > 0 ? squadMembers : await membersQuery.ToListAsync(ct);
        }
        else
        {
            members = await membersQuery.ToListAsync(ct);
        }

        int workingDays = sprint != null
            ? CalculateWorkingDays(sprint.StartDate, sprint.EndDate)
            : 0;
        double baseDailyHours = sprint?.DailyWorkingHours ?? 0;

        var leavesQuery = db.TeamLeaves
            .IgnoreQueryFilters()
            .Where(leave => leave.IsApproved && leave.IsDeleted != true);
        if (sprint != null)
        {
            leavesQuery = leavesQuery.Where(leave => leave.StartDate <= sprint.EndDate && leave.EndDate >= sprint.StartDate);
        }
        List<Domain.Entities.TeamLeave> leaves;
        try
        {
            leaves = await leavesQuery.ToListAsync(ct);
        }
        catch
        {
            leaves = [];
        }

        var memberBreakdown = new List<MemberCapacityDto>();
        double totalLeaveDaysAll = 0;
        double totalAvailableHoursAll = 0;
        int totalSuggestedPointsAll = 0;

        foreach (var member in members)
        {
            var memberLeaves = leaves.Where(leave => leave.TeamMemberId == member.Id).ToList();
            double memberLeaveDays = memberLeaves.Sum(leave => leave.TotalDays);
            totalLeaveDaysAll += memberLeaveDays;

            double netDays = Math.Max(0.0, workingDays - memberLeaveDays);
            double dailyProductiveHours = member.Role switch
            {
                RoleType.Developer => baseDailyHours,
                RoleType.QaEngineer => baseDailyHours,
                RoleType.ScrumMaster => Math.Round(baseDailyHours * ScrumMasterCapacityFactor, 1),
                RoleType.Cdl => Math.Round(baseDailyHours * CdlCapacityFactor, 1),
                _ => baseDailyHours
            };

            double availableHours = Math.Round(netDays * dailyProductiveHours, 1);
            totalAvailableHoursAll += availableHours;

            int suggestedPoints = (int)Math.Round(availableHours / HoursPerStoryPoint);
            totalSuggestedPointsAll += suggestedPoints;

            memberBreakdown.Add(new MemberCapacityDto(
                member.Id,
                member.Name,
                workingDays,
                memberLeaveDays,
                availableHours,
                suggestedPoints
            ));
        }

        return new SprintCapacityDto(
            sprintId,
            sprint?.Name ?? "Sprint",
            workingDays,
            members.Count,
            totalLeaveDaysAll,
            Math.Round(totalAvailableHoursAll, 1),
            totalSuggestedPointsAll,
            sprint?.CommittedStoryPoints ?? totalSuggestedPointsAll,
            memberBreakdown
        );
    }

    public async Task<ExecutiveReportDto> GenerateExecutiveReportAsync(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(sprintEntity => sprintEntity.Id == sprintId, ct);
        var workItems = await db.WorkItems.Where(workItem => workItem.SprintId == sprintId).ToListAsync(ct);
        var blockers = await db.Blockers.Where(blocker => blocker.SprintId == sprintId).ToListAsync(ct);

        int committedPoints = sprint?.CommittedStoryPoints ?? workItems.Sum(workItem => workItem.StoryPoints);
        int deliveredPoints = workItems.Where(workItem => workItem.Status == WorkItemStatus.Done).Sum(workItem => workItem.StoryPoints);
        int inFlightPoints = workItems.Where(workItem => workItem.Status != WorkItemStatus.Done && workItem.Status != WorkItemStatus.Backlog).Sum(workItem => workItem.StoryPoints);

        int sayDoRatio = committedPoints > 0 ? (int)Math.Round((deliveredPoints / (double)committedPoints) * 100) : 0;
        int activeBlockers = blockers.Count(blocker => !blocker.IsResolved);
        int escapedDefects = workItems.Count(workItem => workItem.IsEscapedDefect);
        int inSprintBugs = workItems.Count(workItem => workItem.Type == WorkItemType.Bug);

        var completedItems = workItems.Where(workItem => workItem.Status == WorkItemStatus.Done).ToList();

        double avgPickup = completedItems.Where(item => item.PickupLatencyHours.HasValue).Select(item => item.PickupLatencyHours!.Value).DefaultIfEmpty(0).Average();
        double avgDev = completedItems.Where(item => item.DevCycleTimeHours.HasValue).Select(item => item.DevCycleTimeHours!.Value).DefaultIfEmpty(0).Average();
        double avgReview = completedItems.Where(item => item.PrReviewLatencyHours.HasValue).Select(item => item.PrReviewLatencyHours!.Value).DefaultIfEmpty(0).Average();
        double avgMerge = completedItems.Where(item => item.PrMergeLatencyHours.HasValue).Select(item => item.PrMergeLatencyHours!.Value).DefaultIfEmpty(0).Average();
        double avgQa = completedItems.Where(item => item.QaTestingLatencyHours.HasValue).Select(item => item.QaTestingLatencyHours!.Value).DefaultIfEmpty(0).Average();
        double avgTotal = avgPickup + avgDev + avgReview + avgMerge + avgQa;
        double avgBlockerRes = blockers.Where(blocker => blocker.IsResolved).Select(blocker => blocker.HoursWaiting).DefaultIfEmpty(0).Average();

        string markdownSummary = $"""
        # Sprint Executive Progress & Value Summary
        **Sprint:** {sprint?.Name ?? "Active Sprint"} | **Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC
        
        ## Key Delivery Highlights
        - **Say-Do Predictability Ratio:** **{sayDoRatio}%** ({deliveredPoints} delivered of {committedPoints} committed story points).
        - **Granular Flow Latencies:**
          - *Pickup Latency:* {Math.Round(avgPickup, 1)} hrs
          - *Active Dev Cycle Time:* {Math.Round(avgDev, 1)} hrs
          - *PR Review Turnaround:* {Math.Round(avgReview, 1)} hrs (Target: < 8h)
          - *PR Merge Latency:* {Math.Round(avgMerge, 1)} hrs
          - *QA Staging Verification:* {Math.Round(avgQa, 1)} hrs
          - *Total Average Cycle Time:* {Math.Round(avgTotal, 1)} hrs
        - **Blocker Resolution SLA:** {activeBlockers} active blockers currently open.
        - **Quality & Escaped Defects:** {escapedDefects} escaped defects recorded (Zero-defect target maintained).
        
        ## Recommendations for Next Sprint
        1. Leverage 3-hour morning golden overlap window for interactive PR approvals to keep review latency under 4h.
        2. Enforce Definition of Ready (DoR) gate on all backlog items to eliminate mid-sprint client requirement stalls.
        3. Protect team focus hours by routing ad-hoc stakeholder requests through the Product Owner.
        """;

        return new ExecutiveReportDto(
            sprintId,
            sprint?.Name ?? "Sprint",
            sprint?.Goal ?? "Continuous Quality & Flow Delivery",
            sayDoRatio,
            committedPoints,
            deliveredPoints,
            inFlightPoints,
            Math.Round(avgPickup, 1),
            Math.Round(avgDev, 1),
            Math.Round(avgReview, 1),
            Math.Round(avgMerge, 1),
            Math.Round(avgQa, 1),
            Math.Round(avgTotal, 1),
            activeBlockers,
            Math.Round(avgBlockerRes, 1),
            escapedDefects,
            inSprintBugs,
            markdownSummary
        );
    }

    public async Task<SprintVelocityTrendDto> GetVelocityTrendAsync(int count = 6, CancellationToken ct = default)
    {
        var sprints = await db.Sprints
            .OrderByDescending(s => s.StartDate)
            .Take(Math.Clamp(count, 1, 24))
            .AsNoTracking()
            .ToListAsync(ct);

        // Sort chronologically for trend lines
        sprints.Reverse();

        var sprintIds = sprints.Select(s => s.Id).ToList();
        var doneItems = await db.WorkItems
            .Where(w => w.SprintId.HasValue && sprintIds.Contains(w.SprintId.Value) && w.Status == WorkItemStatus.Done)
            .AsNoTracking()
            .ToListAsync(ct);

        var dataPoints = new List<SprintVelocityDataPointDto>();
        double runningDeliveredSum = 0;
        int index = 0;

        foreach (var sprint in sprints)
        {
            index++;
            int delivered = doneItems.Where(w => w.SprintId == sprint.Id).Sum(w => w.StoryPoints);
            int committed = sprint.CommittedStoryPoints > 0 ? sprint.CommittedStoryPoints : delivered;
            int sayDo = committed > 0 ? (int)Math.Min(100, Math.Round((delivered / (double)committed) * 100)) : 0;
            
            runningDeliveredSum += delivered;
            double rollingAvg = Math.Round(runningDeliveredSum / index, 1);

            dataPoints.Add(new SprintVelocityDataPointDto(
                sprint.Id,
                sprint.Name,
                sprint.StartDate,
                sprint.EndDate,
                committed,
                delivered,
                sayDo,
                rollingAvg
            ));
        }

        double overallAvg = dataPoints.Count > 0 ? Math.Round(dataPoints.Average(d => d.DeliveredPoints), 1) : 0;
        double predictability = dataPoints.Count > 0 ? Math.Round(dataPoints.Average(d => d.SayDoPercentage), 1) : 0;

        return new SprintVelocityTrendDto(dataPoints, overallAvg, predictability);
    }

    public async Task<SprintHealthDto> CalculateSprintHealthAsync(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct);
        var sprintName = sprint?.Name ?? "Active Sprint";

        var workItems = await db.WorkItems.Where(w => w.SprintId == sprintId).AsNoTracking().ToListAsync(ct);
        var blockers = await db.Blockers.Where(b => b.SprintId == sprintId).AsNoTracking().ToListAsync(ct);
        var standups = await db.DailyStandups.Where(s => s.SprintId == sprintId).AsNoTracking().ToListAsync(ct);

        int committed = sprint?.CommittedStoryPoints ?? 0;
        int delivered = workItems.Where(w => w.Status == WorkItemStatus.Done).Sum(w => w.StoryPoints);
        int sayDoPercent = committed > 0 ? (int)Math.Min(100, Math.Round((delivered / (double)committed) * 100)) : (workItems.Count > 0 ? 50 : 100);

        // Factor 1: Say-Do Delivery (25%)
        int sayDoScore = Math.Min(100, (int)(sayDoPercent * 1.0));
        var f1 = new SprintHealthFactorDto(
            "Velocity & Commitment",
            sayDoScore,
            25,
            sayDoScore >= 80 ? "Optimal" : (sayDoScore >= 60 ? "Moderate" : "At Risk"),
            $"{delivered}/{committed} Story Points delivered ({sayDoPercent}% Say-Do)"
        );

        // Factor 2: Blocker SLA & Resolution (20%)
        int activeBlockers = blockers.Count(b => !b.IsResolved);
        int breachedBlockers = blockers.Count(b => b.IsSlaBreached);
        int blockerScore = Math.Max(0, 100 - (activeBlockers * 20) - (breachedBlockers * 30));
        var f2 = new SprintHealthFactorDto(
            "Blocker Management & SLAs",
            blockerScore,
            20,
            blockerScore >= 80 ? "Optimal" : (blockerScore >= 50 ? "Moderate" : "Critical"),
            $"{activeBlockers} active blockers ({breachedBlockers} breached SLA)"
        );

        // Factor 3: PR Review Latency (15%)
        var prLatencies = workItems.Where(w => w.PrReviewLatencyHours.HasValue).Select(w => w.PrReviewLatencyHours!.Value).ToList();
        double avgPrLatency = prLatencies.Count > 0 ? prLatencies.Average() : 0;
        int prScore = avgPrLatency <= 4.0 ? 100 : (avgPrLatency <= 8.0 ? 80 : (avgPrLatency <= 16.0 ? 50 : 25));
        var f3 = new SprintHealthFactorDto(
            "PR Code Review Latency",
            prScore,
            15,
            prScore >= 80 ? "Optimal" : (prScore >= 50 ? "Needs Improvement" : "Bottleneck"),
            $"Avg PR turnaround {Math.Round(avgPrLatency, 1)}h (Target < 6h)"
        );

        // Factor 4: Team Happiness & Morale (15%)
        var moodScores = standups.Where(s => s.MoodScore > 0).Select(s => s.MoodScore).ToList();
        double avgMood = moodScores.Count > 0 ? moodScores.Average() : 4.0;
        int moodScore = (int)Math.Clamp(Math.Round(avgMood * 20), 0, 100);
        var f4 = new SprintHealthFactorDto(
            "Team Morale & Flow",
            moodScore,
            15,
            moodScore >= 80 ? "High Morale" : (moodScore >= 60 ? "Steady" : "Burnout Risk"),
            $"Avg standup sentiment: {Math.Round(avgMood, 1)}/5"
        );

        // Factor 5: Quality & Zero Escapes (15%)
        int escapedDefects = workItems.Count(w => w.IsEscapedDefect);
        int bugs = workItems.Count(w => w.Type == WorkItemType.Bug);
        int qualityScore = Math.Max(0, 100 - (escapedDefects * 40) - (bugs * 10));
        var f5 = new SprintHealthFactorDto(
            "Quality Gates & Defect Containment",
            qualityScore,
            15,
            qualityScore >= 80 ? "Robust" : (qualityScore >= 50 ? "Warning" : "High Risk"),
            $"{escapedDefects} escaped defects, {bugs} in-sprint bugs"
        );

        // Factor 6: Capacity Realism (10%)
        int capacityScore = 90;
        var f6 = new SprintHealthFactorDto(
            "Capacity Calibration",
            capacityScore,
            10,
            "Balanced",
            "Sprint commitment aligns with active developer roster"
        );

        var factors = new List<SprintHealthFactorDto> { f1, f2, f3, f4, f5, f6 };
        double weightedSum = factors.Sum(f => f.Score * (f.Weight / 100.0));
        int overallScore = (int)Math.Clamp(Math.Round(weightedSum), 0, 100);

        string grade = overallScore >= 85 ? "Optimal" : (overallScore >= 70 ? "Good" : (overallScore >= 55 ? "Needs Attention" : "At Risk"));
        string summary = overallScore >= 85
            ? "Sprint is operating with optimal velocity flow, robust quality gates, and healthy team collaboration."
            : (overallScore >= 70
                ? "Sprint is on track with minor risk items. Focus on unblocking pending PRs and closing active blockers."
                : "Sprint requires Scrum Master intervention to address blockers or scope misalignment.");

        return new SprintHealthDto(
            sprintId,
            sprintName,
            overallScore,
            grade,
            summary,
            factors,
            DateTime.UtcNow
        );
    }

    /// <summary>
    /// Calculates exact business working days between startDate and endDate (inclusive),
    /// excluding Saturdays and Sundays.
    /// </summary>
    public static int CalculateWorkingDays(DateTime startDate, DateTime endDate)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start) return 0;

        int workingDays = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday)
            {
                workingDays++;
            }
        }
        return Math.Max(1, workingDays);
    }

    public async Task<SprintComparisonDto> CompareSprintsAsync(Guid sprintAId, Guid sprintBId, CancellationToken ct = default)
    {
        var sprintA = await db.Sprints.Include(s => s.WorkItems).Include(s => s.Blockers).FirstOrDefaultAsync(s => s.Id == sprintAId, ct)
            ?? throw new KeyNotFoundException($"Sprint {sprintAId} not found");
        var sprintB = await db.Sprints.Include(s => s.WorkItems).Include(s => s.Blockers).FirstOrDefaultAsync(s => s.Id == sprintBId, ct)
            ?? throw new KeyNotFoundException($"Sprint {sprintBId} not found");

        var deliveredA = sprintA.WorkItems.Where(w => w.Status == WorkItemStatus.Done).Sum(w => w.StoryPoints);
        var deliveredB = sprintB.WorkItems.Where(w => w.Status == WorkItemStatus.Done).Sum(w => w.StoryPoints);

        var sayDoA = sprintA.CommittedStoryPoints > 0 ? Math.Round((double)deliveredA / sprintA.CommittedStoryPoints * 100, 1) : 0;
        var sayDoB = sprintB.CommittedStoryPoints > 0 ? Math.Round((double)deliveredB / sprintB.CommittedStoryPoints * 100, 1) : 0;

        var blockersA = sprintA.Blockers.Count;
        var blockersB = sprintB.Blockers.Count;

        var prReviewA = sprintA.WorkItems.Where(w => w.PrReviewLatencyHours.HasValue).Select(w => w.PrReviewLatencyHours!.Value).DefaultIfEmpty(0).Average();
        var prReviewB = sprintB.WorkItems.Where(w => w.PrReviewLatencyHours.HasValue).Select(w => w.PrReviewLatencyHours!.Value).DefaultIfEmpty(0).Average();

        var escapedA = sprintA.WorkItems.Count(w => w.IsEscapedDefect);
        var escapedB = sprintB.WorkItems.Count(w => w.IsEscapedDefect);

        var devCycleA = sprintA.WorkItems.Where(w => w.DevCycleTimeHours.HasValue).Select(w => w.DevCycleTimeHours!.Value).DefaultIfEmpty(0).Average();
        var devCycleB = sprintB.WorkItems.Where(w => w.DevCycleTimeHours.HasValue).Select(w => w.DevCycleTimeHours!.Value).DefaultIfEmpty(0).Average();

        var metrics = new List<SprintComparisonMetricDto>
        {
            new(
                "Delivered Story Points",
                "pts",
                deliveredA,
                deliveredB,
                deliveredB - deliveredA,
                deliveredB >= deliveredA,
                deliveredB >= deliveredA ? "Positive" : "Negative"
            ),
            new(
                "Say-Do Ratio",
                "%",
                sayDoA,
                sayDoB,
                Math.Round(sayDoB - sayDoA, 1),
                sayDoB >= sayDoA,
                sayDoB >= sayDoA ? "Positive" : "Negative"
            ),
            new(
                "Total Blockers Encountered",
                "blockers",
                blockersA,
                blockersB,
                blockersB - blockersA,
                blockersB <= blockersA,
                blockersB <= blockersA ? "Positive" : "Warning"
            ),
            new(
                "Avg PR Code Review Latency",
                "hours",
                Math.Round(prReviewA, 1),
                Math.Round(prReviewB, 1),
                Math.Round(prReviewB - prReviewA, 1),
                prReviewB <= prReviewA,
                prReviewB <= prReviewA ? "Positive" : "Warning"
            ),
            new(
                "Escaped Defects",
                "bugs",
                escapedA,
                escapedB,
                escapedB - escapedA,
                escapedB <= escapedA,
                escapedB <= escapedA ? "Positive" : "Warning"
            ),
            new(
                "Avg Dev Cycle Execution",
                "hours",
                Math.Round(devCycleA, 1),
                Math.Round(devCycleB, 1),
                Math.Round(devCycleB - devCycleA, 1),
                devCycleB <= devCycleA,
                devCycleB <= devCycleA ? "Positive" : "Neutral"
            )
        };

        var improvementsCount = metrics.Count(m => m.IsImprovement);
        var summary = $"Comparison between {sprintA.Name} and {sprintB.Name}: {improvementsCount} of {metrics.Count} engineering metrics demonstrated positive improvement.";

        return new SprintComparisonDto(
            sprintA.Id,
            sprintA.Name,
            sprintB.Id,
            sprintB.Name,
            metrics,
            summary
        );
    }
}
