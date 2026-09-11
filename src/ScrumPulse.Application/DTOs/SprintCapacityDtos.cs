namespace ScrumPulse.Application.DTOs;

public record SprintCapacityDto(
    Guid SprintId,
    string SprintName,
    int TotalWorkingDays,
    int TotalTeamMembers,
    double TotalLeaveDays,
    double TotalAvailableHours,
    int RecommendedStoryPoints,
    int CommittedStoryPoints,
    List<MemberCapacityDto> MemberBreakdown,
    double TotalBlockerHours = 0,
    double NetAvailableHours = 0
);

public record MemberCapacityDto(
    Guid MemberId,
    string MemberName,
    int WorkingDays,
    double LeaveDays,
    double AvailableHours,
    int SuggestedPoints,
    double BlockerHours = 0,
    double NetAvailableHours = 0
);
