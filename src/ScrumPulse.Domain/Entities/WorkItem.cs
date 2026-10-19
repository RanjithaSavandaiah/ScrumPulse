namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

public class WorkItem : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public WorkItemType Type { get; set; } = WorkItemType.UserStory;
    public WorkItemStatus Status { get; set; } = WorkItemStatus.Backlog;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public int StoryPoints { get; set; } = 3;
    public double? EstimatedHours { get; set; }

    public Guid? AssigneeId { get; set; }
    public TeamMember? Assignee { get; set; }

    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public string? PrNumber { get; set; }
    public string? PrUrl { get; set; }
    public string? PrBranch { get; set; }
    public string? TargetBranch { get; set; } = "main";
    public Guid? PrReviewerId { get; set; }
    public TeamMember? PrReviewer { get; set; }

    public DateTime? PickedUpAtUtc { get; set; }
    public DateTime? PrCreatedAtUtc { get; set; }
    public DateTime? PrApprovedAtUtc { get; set; }
    public DateTime? PrMergedAtUtc { get; set; }
    public DateTime? QaStartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public bool DorAcceptanceCriteriaDefined { get; set; } = true;
    public bool DorDependenciesIdentified { get; set; } = true;
    public bool DorWireframeAvailable { get; set; } = true;
    public bool DodUnitTestsPassed { get; set; } = false;
    public bool DodPeerReviewCompleted { get; set; } = false;
    public bool DodMergedToMaster { get; set; } = false;
    public bool DodStagingVerified { get; set; } = false;
    public string? QualityGateResultsJson { get; set; }

    public bool IsEscapedDefect { get; set; } = false;
    public string? DefectRootCause { get; set; }

    // -- Delivery board origin --------------------------------------------------------------

    /// <summary>
    /// Anything other than Manual is owned by the source board; local edits are overwritten by
    /// the next sync and leave two answers to the same question.
    /// </summary>
    public WorkItemOrigin Origin { get; set; } = WorkItemOrigin.Manual;

    /// <summary>
    /// Source board identifier. Matching on this rather than the title keeps repeated syncs
    /// idempotent, since a renamed story would otherwise import twice.
    /// </summary>
    public string? ExternalId { get; set; }

    // Stored only when the content policy permits: the URL carries the client's organisation
    // and project names.
    public string? ExternalUrl { get; set; }

    /// <summary>The board's own state name, so an unmapped column can be reported by name.</summary>
    public string? BoardState { get; set; }

    public DateTime? LastSyncedAtUtc { get; set; }

    public bool IsReadOnlyFromBoard => Origin != WorkItemOrigin.Manual;

    // Computed Latencies (Working Hours Only)
    public double? PickupLatencyHours => PickedUpAtUtc.HasValue 
        ? WorkingHoursCalculator.CalculateWorkingHours(CreatedAtUtc, PickedUpAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;
    public double? DevCycleTimeHours => (PrCreatedAtUtc.HasValue && PickedUpAtUtc.HasValue) 
        ? WorkingHoursCalculator.CalculateWorkingHours(PickedUpAtUtc.Value, PrCreatedAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;
    public double? PrReviewLatencyHours => (PrApprovedAtUtc.HasValue && PrCreatedAtUtc.HasValue) 
        ? WorkingHoursCalculator.CalculateWorkingHours(PrCreatedAtUtc.Value, PrApprovedAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;
    public double? PrMergeLatencyHours => (PrMergedAtUtc.HasValue && PrApprovedAtUtc.HasValue) 
        ? WorkingHoursCalculator.CalculateWorkingHours(PrApprovedAtUtc.Value, PrMergedAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;
    public double? QaTestingLatencyHours => (CompletedAtUtc.HasValue && QaStartedAtUtc.HasValue) 
        ? WorkingHoursCalculator.CalculateWorkingHours(QaStartedAtUtc.Value, CompletedAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;
    public double? TotalCycleTimeHours => (CompletedAtUtc.HasValue && PickedUpAtUtc.HasValue) 
        ? WorkingHoursCalculator.CalculateWorkingHours(PickedUpAtUtc.Value, CompletedAtUtc.Value, Sprint?.DailyWorkingHours ?? WorkingHoursCalculator.DefaultDailyWorkingHours) 
        : null;

    // Status Aging & Bottleneck Tracking
    public DateTime StatusEnteredAtUtc => Status switch
    {
        WorkItemStatus.Done => CompletedAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        WorkItemStatus.InQa => QaStartedAtUtc ?? PrMergedAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        WorkItemStatus.Merged => PrMergedAtUtc ?? PrApprovedAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        WorkItemStatus.PrApproved => PrApprovedAtUtc ?? PrCreatedAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        WorkItemStatus.PrCreated => PrCreatedAtUtc ?? PickedUpAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        WorkItemStatus.InProgress => PickedUpAtUtc ?? UpdatedAtUtc ?? CreatedAtUtc,
        _ => CreatedAtUtc
    };

    public double DaysInCurrentStatus => Status == WorkItemStatus.Done
        ? 0
        : Math.Round(Math.Max(0, (DateTime.UtcNow - StatusEnteredAtUtc).TotalDays), 1);
}
