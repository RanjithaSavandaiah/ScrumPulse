namespace ScrumPulse.Application.Mapping;

using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using System.Text.Json;

/// <summary>
/// Centralized, zero-allocation extension methods for entity-to-DTO mapping.
/// Eliminates 40+ duplicate inline mapping blocks across handlers and controllers.
/// </summary>
public static class MappingExtensions
{
    // ── WorkItem ─────────────────────────────────────────────────────────

    public static WorkItemDto ToDto(this WorkItem item) => new(
        item.Id, item.Key, item.Title, item.Description,
        item.Type, item.Status, item.Priority, item.StoryPoints,
        item.AssigneeId, item.Assignee?.Name, item.SprintId,
        item.PrNumber, item.PrUrl, item.PrBranch, item.TargetBranch,
        item.PrReviewerId, item.PrReviewer?.Name,
        item.CreatedAtUtc, item.PickedUpAtUtc, item.PrCreatedAtUtc,
        item.PrApprovedAtUtc, item.PrMergedAtUtc, item.QaStartedAtUtc,
        item.CompletedAtUtc,
        item.DorAcceptanceCriteriaDefined, item.DorDependenciesIdentified,
        item.DorWireframeAvailable, item.DodUnitTestsPassed,
        item.DodPeerReviewCompleted, item.DodMergedToMaster,
        item.DodStagingVerified, item.IsEscapedDefect, item.DefectRootCause,
        item.PickupLatencyHours, item.DevCycleTimeHours,
        item.PrReviewLatencyHours, item.PrMergeLatencyHours,
        item.QaTestingLatencyHours, item.TotalCycleTimeHours,
        item.EstimatedHours
    );

    public static IEnumerable<WorkItemDto> ToDtos(this IEnumerable<WorkItem> items) =>
        items.Select(item => item.ToDto());

    // ── Blocker ──────────────────────────────────────────────────────────

    public static BlockerDto ToDto(this Blocker blocker) => new(
        blocker.Id, blocker.Title, blocker.Description, blocker.Category,
        blocker.SlaHoursLimit, blocker.WorkItemId, blocker.WorkItem?.Key,
        blocker.RaisedById, blocker.RaisedBy?.Name, blocker.SprintId,
        blocker.RaisedAtUtc, blocker.ResolvedAtUtc, blocker.ResolutionNotes,
        blocker.IsResolved, blocker.HoursWaiting, blocker.IsSlaBreached
    );

    public static IEnumerable<BlockerDto> ToDtos(this IEnumerable<Blocker> blockers) =>
        blockers.Select(blocker => blocker.ToDto());

    // ── DailyStandup ─────────────────────────────────────────────────────

    public static DailyStandupDto ToDto(this DailyStandup standup) => new(
        standup.Id, standup.TeamMemberId,
        standup.TeamMember?.Name ?? "Member",
        standup.TeamMember?.Avatar ?? "",
        standup.StandupDate, standup.YesterdaySummary, standup.TodayPlan,
        standup.BlockersText, standup.MoodScore, standup.SprintId
    );

    public static IEnumerable<DailyStandupDto> ToDtos(this IEnumerable<DailyStandup> standups) =>
        standups.Select(standup => standup.ToDto());

    // ── TeamLeave ────────────────────────────────────────────────────────

    public static TeamLeaveDto ToDto(this TeamLeave leave) => new(
        leave.Id, leave.TeamMemberId,
        leave.TeamMember?.Name ?? "Member",
        leave.StartDate, leave.EndDate, leave.Reason,
        leave.LeaveType.ToString(), leave.Location, leave.IsApproved,
        leave.TotalDays, leave.LeaveSlot.ToString()
    );

    public static IEnumerable<TeamLeaveDto> ToDtos(this IEnumerable<TeamLeave> leaves) =>
        leaves.Select(leave => leave.ToDto());

    // ── MonthlyFeedback ──────────────────────────────────────────────────

    public static MonthlyFeedbackDto ToDto(this Monthly1on1Feedback feedback) => new(
        feedback.Id, feedback.TeamMemberId,
        feedback.TeamMember?.Name ?? "Member",
        feedback.MonthYear, feedback.ScrumMasterFeedback, feedback.CdlFeedback,
        feedback.ClientFeedback, feedback.SelfReflection, feedback.SmRating,
        feedback.HappinessIndex, feedback.ActionItems, feedback.NextMonthGoals,
        feedback.AiSynthesizedStrengths, feedback.AiGrowthRecommendations,
        feedback.AiBurnoutRiskAssessment, feedback.CreatedAtUtc
    );

    public static IEnumerable<MonthlyFeedbackDto> ToDtos(this IEnumerable<Monthly1on1Feedback> feedbacks) =>
        feedbacks.Select(feedback => feedback.ToDto());

    // ── RetroCard ────────────────────────────────────────────────────────

    public static RetroCardDto ToDto(this RetroCard card) => new(
        card.Id, card.SprintId, card.Category, card.Content, card.AuthorId,
        card.IsAnonymous ? "Anonymous" : card.Author?.Name,
        card.IsAnonymous, card.UpvotesCount,
        JsonSerializer.Deserialize<List<Guid>>(card.UpvoterMemberIdsJson) ?? []
    );

    public static IEnumerable<RetroCardDto> ToDtos(this IEnumerable<RetroCard> cards) =>
        cards.Select(card => card.ToDto());

    // ── RetroActionItem ──────────────────────────────────────────────────

    public static RetroActionItemDto ToDto(this RetroActionItem item) => new(
        item.Id, item.SprintId, item.Title, item.AssigneeId,
        item.Assignee?.Name, item.DueDate, item.IsCompleted
    );

    public static IEnumerable<RetroActionItemDto> ToDtos(this IEnumerable<RetroActionItem> items) =>
        items.Select(item => item.ToDto());

    // ── KudosCard ────────────────────────────────────────────────────────

    public static KudosDto ToDto(this KudosCard kudos) => new(
        kudos.Id, kudos.SenderId, kudos.Sender?.Name ?? "Teammate",
        kudos.ReceiverId, kudos.Receiver?.Name ?? "Teammate",
        kudos.Badge, kudos.Message,
        JsonSerializer.Deserialize<Dictionary<string, int>>(kudos.ReactionEmojisJson) ?? [],
        kudos.CreatedAtUtc
    );

    public static IEnumerable<KudosDto> ToDtos(this IEnumerable<KudosCard> cards) =>
        cards.Select(card => card.ToDto());

    // ── PullRequestReviewLog ─────────────────────────────────────────────

    public static PullRequestLogDto ToDto(this PullRequestReviewLog log) => new(
        log.Id, log.WorkItemId, log.WorkItem?.Title,
        log.AuthorId, log.Author?.Name ?? "Unknown",
        log.ReviewerId, log.Reviewer?.Name,
        log.SprintId, log.Sprint?.Name,
        log.PrNumber, log.PrTitle, log.PrUrl,
        log.TotalCommentsCount, log.ActionableCommentsCount,
        log.ReviewSummary, log.ReviewStatus.ToString(),
        log.CreatedAtUtc, log.MergedAtUtc
    );

    public static IEnumerable<PullRequestLogDto> ToDtos(this IEnumerable<PullRequestReviewLog> logs) =>
        logs.Select(log => log.ToDto());

    // ── TechDebtItem (new typed DTOs) ────────────────────────────────────

    public static TechDebtItemDto ToDto(this TechDebtItem item) => new(
        item.Id, item.Title, item.Description,
        item.Severity, item.EstimatedHours, item.Status,
        item.PayoffSprintId, item.AssigneeId,
        item.Assignee?.Name, item.CreatedAtUtc
    );

    public static IEnumerable<TechDebtItemDto> ToDtos(this IEnumerable<TechDebtItem> items) =>
        items.Select(item => item.ToDto());

    // ── TechTalkLog (new typed DTOs) ─────────────────────────────────────

    public static TechTalkLogDto ToDto(this TechTalkLog log) => new(
        log.Id, log.Topic, log.PresenterId,
        log.Presenter?.Name, log.TalkDate,
        log.DurationMinutes, log.KeyTakeaways, log.SlidesUrl
    );

    public static IEnumerable<TechTalkLogDto> ToDtos(this IEnumerable<TechTalkLog> logs) =>
        logs.Select(log => log.ToDto());
}
