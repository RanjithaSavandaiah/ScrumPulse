namespace ScrumPulse.Application.DTOs;

/// <summary>
/// Complete team performance summary designed for client facing presentations
/// in service based delivery organizations.
/// </summary>
public record TeamPerformanceSummaryDto(
    string TeamName,
    string PerformanceGrade,
    int OverallScore,
    string Headline,
    int SprintsAnalyzed,
    DateTime EvaluatedAtUtc,
    IReadOnlyList<GrowthMetricDto> Metrics,
    IReadOnlyList<SprintGrowthSnapshotDto> SprintSnapshots,
    IReadOnlyList<TeamHighlightDto> Highlights,
    TeamEngagementDto Engagement
);

/// <summary>Single KPI metric with trend and client friendly labeling.</summary>
public record GrowthMetricDto(
    string MetricName,
    string Category,
    double CurrentValue,
    double PreviousValue,
    double DeltaPercent,
    string TrendDirection,
    string Unit,
    string ClientLabel,
    string Icon
);

/// <summary>Per sprint performance data point for trend visualization.</summary>
public record SprintGrowthSnapshotDto(
    Guid SprintId,
    string SprintName,
    DateTime StartDate,
    DateTime EndDate,
    int DeliveredPoints,
    int CommittedPoints,
    double SayDoPercent,
    int EscapedDefects,
    double AvgPrReviewHours,
    int BlockersRaised,
    int BlockersResolved,
    double TeamMoodAvg
);

/// <summary>Auto generated client ready highlight statement.</summary>
public record TeamHighlightDto(
    string Icon,
    string Category,
    string Statement,
    string Sentiment
);

/// <summary>Team engagement and culture metrics.</summary>
public record TeamEngagementDto(
    double AvgMoodScore,
    int TotalKudosGiven,
    int TechTalksDelivered,
    int TechDebtItemsResolved,
    double KudosPerSprint,
    double TechTalksPerSprint,
    string EngagementGrade
);
