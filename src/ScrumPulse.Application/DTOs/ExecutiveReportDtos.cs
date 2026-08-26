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
