namespace ScrumPulse.AI.Retrieval;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.AI.Strategies;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Retrieves delivery metrics from the database for the agents to reason over.
/// </summary>
/// <remarks>
/// <para>
/// Aggregation happens in SQL rather than in memory. The previous implementation pulled whole
/// tables back with <c>ToListAsync</c> and then summed and averaged them with LINQ to Objects,
/// so building one member's context transferred every work item, leave record and standup the
/// squad had ever produced. Counting in the database keeps the transfer to a single row of
/// numbers, which matters on a free Postgres tier where the connection is the bottleneck.
/// </para>
/// <para>
/// Every query here runs through the tenant query filter on the DbContext, so it is
/// automatically scoped to the requesting session's squad.
/// </para>
/// </remarks>
public sealed class DeliveryMetricsRetriever(IAppDbContext dbContext) : IDeliveryMetricsRetriever
{
    /// <summary>Response target for a blocker, in hours, before it counts as breaching SLA.</summary>
    private const double BlockerSlaHours = 8;

    /// <inheritdoc />
    public async Task<InsightContext> BuildMemberContextAsync(Guid memberId, CancellationToken ct = default)
    {
        var memberName = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => member.Id == memberId)
            .Select(member => member.Name)
            .FirstOrDefaultAsync(ct);

        // One grouped query instead of fetching the rows and reducing them client side.
        // DefaultIfEmpty guards the averages: averaging an empty sequence throws in SQL too.
        var delivery = await dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.AssigneeId == memberId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalAssigned = group.Count(),
                Completed = group.Count(workItem => workItem.Status == WorkItemStatus.Done),
                StoryPoints = group
                    .Where(workItem => workItem.Status == WorkItemStatus.Done)
                    .Sum(workItem => (int?)workItem.StoryPoints) ?? 0,
                AvgDevCycleHours = group
                    .Where(workItem => workItem.DevCycleTimeHours != null)
                    .Average(workItem => (double?)workItem.DevCycleTimeHours) ?? 0,
                AvgReviewLatencyHours = group
                    .Where(workItem => workItem.PrReviewLatencyHours != null)
                    .Average(workItem => (double?)workItem.PrReviewLatencyHours) ?? 0
            })
            .FirstOrDefaultAsync(ct);

        var approvedLeaveDays = await dbContext.TeamLeaves
            .AsNoTracking()
            .Where(leave => leave.TeamMemberId == memberId && leave.IsApproved)
            .SumAsync(leave => (double?)leave.TotalDays, ct) ?? 0;

        var standupCount = await dbContext.DailyStandups
            .AsNoTracking()
            .CountAsync(standup => standup.TeamMemberId == memberId, ct);

        var techTalksGiven = await dbContext.TechTalkLogs
            .AsNoTracking()
            .CountAsync(talk => talk.PresenterId == memberId, ct);

        var kudosReceived = await dbContext.KudosCards
            .AsNoTracking()
            .CountAsync(kudos => kudos.ReceiverId == memberId, ct);

        var latestFeedback = await dbContext.Monthly1on1Feedbacks
            .AsNoTracking()
            .Where(feedback => feedback.TeamMemberId == memberId)
            .OrderByDescending(feedback => feedback.CreatedAtUtc)
            .Select(feedback => new
            {
                feedback.HappinessIndex,
                feedback.SmRating,
                feedback.ActionItems
            })
            .FirstOrDefaultAsync(ct);

        return new InsightContext
        {
            MemberId = memberId,
            MemberName = string.IsNullOrWhiteSpace(memberName) ? "this engineer" : memberName,
            TotalAssigned = delivery?.TotalAssigned ?? 0,
            CompletedItems = delivery?.Completed ?? 0,
            TotalStoryPoints = delivery?.StoryPoints ?? 0,
            AvgDevCycleHours = delivery?.AvgDevCycleHours ?? 0,
            AvgReviewLatencyHours = delivery?.AvgReviewLatencyHours ?? 0,
            TotalLeaveDays = approvedLeaveDays,
            NetCapacityHours = await CalculateNetCapacityHoursAsync(approvedLeaveDays, ct),
            StandupCount = standupCount,
            TechTalksGiven = techTalksGiven,
            KudosReceived = kudosReceived,
            HappinessIndex = latestFeedback?.HappinessIndex ?? 0,
            SmRating = latestFeedback?.SmRating ?? 0,
            LastActionItems = latestFeedback?.ActionItems
        };
    }

    /// <inheritdoc />
    public async Task<InsightContext> BuildSprintContextAsync(Guid sprintId, CancellationToken ct = default)
    {
        var sprint = await dbContext.Sprints
            .AsNoTracking()
            .Where(candidate => candidate.Id == sprintId)
            .Select(candidate => new
            {
                candidate.Name,
                candidate.CommittedStoryPoints,
                candidate.ConfidenceScore
            })
            .FirstOrDefaultAsync(ct);

        var deliveredPoints = await dbContext.WorkItems
            .AsNoTracking()
            .Where(workItem => workItem.SprintId == sprintId && workItem.Status == WorkItemStatus.Done)
            .SumAsync(workItem => (int?)workItem.StoryPoints, ct) ?? 0;

        var activeBlockers = await dbContext.Blockers
            .AsNoTracking()
            // Filtering on ResolvedAtUtc rather than the IsResolved property: IsResolved is
            // computed in C# and has no column, so EF cannot translate it and the query would
            // throw at runtime.
            .CountAsync(blocker => blocker.SprintId == sprintId && blocker.ResolvedAtUtc == null, ct);

        var leaveDays = await dbContext.TeamLeaves
            .AsNoTracking()
            .Where(leave => leave.IsApproved)
            .SumAsync(leave => (double?)leave.TotalDays, ct) ?? 0;

        var morale = await dbContext.Monthly1on1Feedbacks
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                AvgHappiness = group.Average(feedback => (double?)feedback.HappinessIndex) ?? 0,
                AvgSmRating = group.Average(feedback => (double?)feedback.SmRating) ?? 0
            })
            .FirstOrDefaultAsync(ct);

        var techTalks = await dbContext.TechTalkLogs.AsNoTracking().CountAsync(ct);

        return new InsightContext
        {
            SprintId = sprintId,
            SprintName = string.IsNullOrWhiteSpace(sprint?.Name) ? "the current sprint" : sprint.Name,
            CommittedPoints = sprint?.CommittedStoryPoints ?? 0,
            DeliveredPoints = deliveredPoints,
            ConfidenceScore = sprint?.ConfidenceScore ?? 0,
            ActiveBlockers = activeBlockers,
            TotalLeaveDays = leaveDays,
            AvgTeamHappiness = morale?.AvgHappiness ?? 0,
            AvgSmRating = morale?.AvgSmRating ?? 0,
            TotalTechTalks = techTalks
        };
    }

    /// <inheritdoc />
    public async Task<InsightContext> BuildPortfolioContextAsync(CancellationToken ct = default)
    {
        var workItems = await dbContext.WorkItems
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Completed = group.Count(workItem => workItem.Status == WorkItemStatus.Done),
                StoryPoints = group
                    .Where(workItem => workItem.Status == WorkItemStatus.Done)
                    .Sum(workItem => (int?)workItem.StoryPoints) ?? 0,
                EscapedDefects = group.Count(workItem =>
                    workItem.Type == WorkItemType.Bug && workItem.IsEscapedDefect)
            })
            .FirstOrDefaultAsync(ct);

        var blockers = await dbContext.Blockers
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Resolved = group.Count(blocker => blocker.ResolvedAtUtc != null)
            })
            .FirstOrDefaultAsync(ct);

        return new InsightContext
        {
            SprintsCount = await dbContext.Sprints.AsNoTracking().CountAsync(ct),
            TotalWorkItems = workItems?.Total ?? 0,
            CompletedItems = workItems?.Completed ?? 0,
            TotalStoryPoints = workItems?.StoryPoints ?? 0,
            EscapedDefects = workItems?.EscapedDefects ?? 0,
            TotalBlockers = blockers?.Total ?? 0,
            ResolvedBlockers = blockers?.Resolved ?? 0,
            ActiveBlockers = (blockers?.Total ?? 0) - (blockers?.Resolved ?? 0),
            TotalKudos = await dbContext.KudosCards.AsNoTracking().CountAsync(ct),
            TotalTechTalks = await dbContext.TechTalkLogs.AsNoTracking().CountAsync(ct)
        };
    }

    /// <inheritdoc />
    public async Task<InsightContext?> BuildActiveSprintContextAsync(CancellationToken ct = default)
    {
        var activeSprintId = await dbContext.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.IsActive)
            .Select(sprint => (Guid?)sprint.Id)
            .FirstOrDefaultAsync(ct);

        return activeSprintId is null
            ? null
            : await BuildSprintContextAsync(activeSprintId.Value, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BlockerSummary>> GetOpenBlockersAsync(
        int maximumResults,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var openBlockers = await dbContext.Blockers
            .AsNoTracking()
            // ResolvedAtUtc rather than the IsResolved property: IsResolved is computed in C#
            // and has no column, so EF cannot translate it into SQL.
            .Where(blocker => blocker.ResolvedAtUtc == null)
            .OrderBy(blocker => blocker.RaisedAtUtc)
            .Take(maximumResults)
            .Select(blocker => new
            {
                blocker.Title,
                blocker.Category,
                blocker.RaisedAtUtc,
                blocker.SlaHoursLimit
            })
            .ToListAsync(ct);

        return
        [
            .. openBlockers.Select(blocker =>
            {
                var hoursWaiting = (now - blocker.RaisedAtUtc).TotalHours;

                return new BlockerSummary(
                    blocker.Title,
                    blocker.Category.ToString(),
                    Math.Round(hoursWaiting, 1),
                    hoursWaiting > blocker.SlaHoursLimit);
            })
        ];
    }

    /// <inheritdoc />
    public async Task<MemberReference?> FindMemberByNameAsync(
        string partialName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(partialName))
        {
            return null;
        }

        var searchTerm = partialName.Trim();

        var match = await dbContext.TeamMembers
            .AsNoTracking()
            .Where(member => EF.Functions.Like(member.Name, $"%{searchTerm}%"))
            .Select(member => new { member.Id, member.Name })
            .FirstOrDefaultAsync(ct);

        return match is null ? null : new MemberReference(match.Id, match.Name);
    }

    /// <summary>
    /// Works out how many hours a member has left in the active sprint after approved leave.
    /// </summary>
    /// <remarks>
    /// Returns zero when no sprint is active, which the prompt renders as "not measurable"
    /// rather than implying the member has no capacity at all.
    /// </remarks>
    private async Task<double> CalculateNetCapacityHoursAsync(double approvedLeaveDays, CancellationToken ct)
    {
        var activeSprint = await dbContext.Sprints
            .AsNoTracking()
            .Where(sprint => sprint.IsActive)
            .Select(sprint => new { sprint.StartDate, sprint.EndDate, sprint.DailyWorkingHours })
            .FirstOrDefaultAsync(ct);

        if (activeSprint is null || activeSprint.DailyWorkingHours <= 0)
        {
            return 0;
        }

        var workingDays = WorkingHoursCalculator.CountWorkingDays(
            activeSprint.StartDate,
            activeSprint.EndDate);

        return Math.Max(0, (workingDays - approvedLeaveDays) * activeSprint.DailyWorkingHours);
    }
}
