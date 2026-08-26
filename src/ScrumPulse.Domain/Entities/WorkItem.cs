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

    public bool IsEscapedDefect { get; set; } = false;
    public string? DefectRootCause { get; set; }

    // Computed Latencies
    public double? PickupLatencyHours => PickedUpAtUtc.HasValue ? Math.Round((PickedUpAtUtc.Value - CreatedAtUtc).TotalHours, 1) : null;
    public double? DevCycleTimeHours => (PrCreatedAtUtc.HasValue && PickedUpAtUtc.HasValue) ? Math.Round((PrCreatedAtUtc.Value - PickedUpAtUtc.Value).TotalHours, 1) : null;
    public double? PrReviewLatencyHours => (PrApprovedAtUtc.HasValue && PrCreatedAtUtc.HasValue) ? Math.Round((PrApprovedAtUtc.Value - PrCreatedAtUtc.Value).TotalHours, 1) : null;
    public double? PrMergeLatencyHours => (PrMergedAtUtc.HasValue && PrApprovedAtUtc.HasValue) ? Math.Round((PrMergedAtUtc.Value - PrApprovedAtUtc.Value).TotalHours, 1) : null;
    public double? QaTestingLatencyHours => (CompletedAtUtc.HasValue && QaStartedAtUtc.HasValue) ? Math.Round((CompletedAtUtc.Value - QaStartedAtUtc.Value).TotalHours, 1) : null;
    public double? TotalCycleTimeHours => (CompletedAtUtc.HasValue && PickedUpAtUtc.HasValue) ? Math.Round((CompletedAtUtc.Value - PickedUpAtUtc.Value).TotalHours, 1) : null;
}
