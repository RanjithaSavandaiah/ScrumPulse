namespace ScrumPulse.Application.DTOs;

public record ExecutiveReportDto(
    Guid SprintId,
    string SprintName,
    string SprintGoal,
    int SayDoRatioPercentage,
    int CommittedPoints,
    int DeliveredPoints,
    int InFlightPoints,
    double AvgPickupLatencyHours,
    double AvgDevTimeHours,
    double AvgPrReviewHours,
    double AvgPrMergeHours,
    double AvgQaTestingHours,
    double AvgTotalCycleTimeHours,
    int ActiveBlockersCount,
    double AvgBlockerResolutionHours,
    int EscapedDefectsCount,
    int InSprintBugsCount,
    string ExecutiveSummaryMarkdown
);

public record SprintVelocityDataPointDto(
    Guid SprintId,
    string SprintName,
    DateTime StartDate,
    DateTime EndDate,
    int CommittedPoints,
    int DeliveredPoints,
    int SayDoPercentage,
    double RollingAverageVelocity
);

public record SprintVelocityTrendDto(
    IReadOnlyList<SprintVelocityDataPointDto> Sprints,
    double AverageVelocity,
    double PredictabilityScore
);

public record SprintHealthFactorDto(
    string Dimension,
    int Score,
    int Weight,
    string Status,
    string Details
);

public record SprintHealthDto(
    Guid SprintId,
    string SprintName,
    int OverallScore,
    string HealthGrade,
    string StatusSummary,
    IReadOnlyList<SprintHealthFactorDto> Factors,
    DateTime EvaluatedAtUtc
);

public record SprintComparisonMetricDto(
    string MetricName,
    string Unit,
    double ValueSprintA,
    double ValueSprintB,
    double Delta,
    bool IsImprovement,
    string Sentiment
);

public record SprintComparisonDto(
    Guid SprintAId,
    string SprintAName,
    Guid SprintBId,
    string SprintBName,
    IReadOnlyList<SprintComparisonMetricDto> Metrics,
    string ComparisonSummary
);
