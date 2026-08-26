namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record DailyStandupDto(
    Guid Id,
    Guid TeamMemberId,
    string TeamMemberName,
    string TeamMemberAvatar,
    DateTime StandupDate,
    string YesterdaySummary,
    string TodayPlan,
    string? BlockersText,
    int MoodScore,
    Guid? SprintId = null
);

public record SubmitStandupRequest(
    [Required] Guid TeamMemberId,
    [Required][StringLength(1000)] string YesterdaySummary,
    [Required][StringLength(1000)] string TodayPlan,
    [StringLength(1000)] string? BlockersText,
    [Range(1, 5)] int MoodScore,
    Guid? SprintId
);
