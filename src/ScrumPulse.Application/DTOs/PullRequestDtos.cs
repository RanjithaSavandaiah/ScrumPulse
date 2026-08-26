namespace ScrumPulse.Application.DTOs;

public record PullRequestLogDto(
    Guid Id,
    Guid? WorkItemId,
    string? WorkItemTitle,
    Guid AuthorId,
    string AuthorName,
    Guid? ReviewerId,
    string? ReviewerName,
    Guid? SprintId,
    string? SprintName,
    string PrNumber,
    string PrTitle,
    string PrUrl,
    int TotalCommentsCount,
    int ActionableCommentsCount,
    string ReviewSummary,
    string ReviewStatus,
    DateTime CreatedAtUtc,
    DateTime? MergedAtUtc
);

public record CreatePullRequestLogRequest(
    Guid? WorkItemId,
    Guid AuthorId,
    Guid? ReviewerId,
    Guid? SprintId,
    string PrNumber,
    string PrTitle,
    string PrUrl,
    int TotalCommentsCount,
    int ActionableCommentsCount,
    string ReviewSummary,
    string ReviewStatus
);

public record DeveloperPrMetricsDto(
    Guid DeveloperId,
    string DeveloperName,
    string DeveloperRole,
    string DeveloperAvatar,
    int TotalPrsCreated,
    int TotalCommentsReceived,
    int ActionableCommentsReceived,
    double ActionabilityRatePercentage,
    double AvgCommentsPerPr,
    List<PullRequestLogDto> Prs
);
