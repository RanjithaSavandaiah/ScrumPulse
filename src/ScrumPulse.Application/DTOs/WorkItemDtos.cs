namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

public record WorkItemDto(
    Guid Id,
    string Key,
    string Title,
    string Description,
    WorkItemType Type,
    WorkItemStatus Status,
    PriorityLevel Priority,
    int StoryPoints,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? SprintId,
    string? PrNumber,
    string? PrUrl,
    string? PrBranch,
    string? TargetBranch,
    Guid? PrReviewerId,
    string? PrReviewerName,
    DateTime CreatedAtUtc,
    DateTime? PickedUpAtUtc,
    DateTime? PrCreatedAtUtc,
    DateTime? PrApprovedAtUtc,
    DateTime? PrMergedAtUtc,
    DateTime? QaStartedAtUtc,
    DateTime? CompletedAtUtc,
    bool DorAcceptanceCriteriaDefined,
    bool DorDependenciesIdentified,
    bool DorWireframeAvailable,
    bool DodUnitTestsPassed,
    bool DodPeerReviewCompleted,
    bool DodMergedToMaster,
    bool DodStagingVerified,
    bool IsEscapedDefect,
    string? DefectRootCause,
    double? PickupLatencyHours,
    double? DevCycleTimeHours,
    double? PrReviewLatencyHours,
    double? PrMergeLatencyHours,
    double? QaTestingLatencyHours,
    double? TotalCycleTimeHours,
    double? EstimatedHours = null
);

public record CreateWorkItemRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    [Required][StringLength(4000)] string Description,
    WorkItemType Type,
    PriorityLevel Priority,
    [Range(0, 100)] int StoryPoints,
    Guid? AssigneeId,
    Guid? SprintId,
    [StringLength(50)] string? PrNumber,
    [StringLength(500)] string? PrUrl,
    [StringLength(100)] string? PrBranch,
    [StringLength(100)] string? TargetBranch,
    [Range(0, 1000)] double? EstimatedHours = null
);

public record UpdateWorkItemRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    [Required][StringLength(4000)] string Description,
    WorkItemType Type,
    PriorityLevel Priority,
    [Range(0, 100)] int StoryPoints,
    Guid? AssigneeId,
    Guid? SprintId,
    [StringLength(50)] string? PrNumber,
    [StringLength(500)] string? PrUrl,
    [StringLength(100)] string? PrBranch,
    [StringLength(100)] string? TargetBranch,
    [Range(0, 1000)] double? EstimatedHours = null
);

public record AdvanceStageRequest(
    WorkItemStatus TargetStatus,
    DateTime? CustomTimestampUtc = null,
    [StringLength(50)] string? PrNumber = null,
    [StringLength(500)] string? PrUrl = null,
    Guid? ReviewerId = null
);

public record UpdateQualityGatesRequest(
    bool DorAcceptanceCriteria,
    bool DorDependencies,
    bool DorWireframe,
    bool DodUnitTests,
    bool DodPeerReview,
    bool DodMergedToMaster,
    bool DodStagingVerified
);
