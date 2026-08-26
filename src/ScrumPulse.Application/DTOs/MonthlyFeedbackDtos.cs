namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record MonthlyFeedbackDto(
    Guid Id,
    Guid TeamMemberId,
    string TeamMemberName,
    string MonthYear,
    string ScrumMasterFeedback,
    string CdlFeedback,
    string ClientFeedback,
    string SelfReflection,
    int SmRating,
    int HappinessIndex,
    string ActionItems,
    string NextMonthGoals,
    string? AiSynthesizedStrengths,
    string? AiGrowthRecommendations,
    string? AiBurnoutRiskAssessment,
    DateTime CreatedAtUtc
);

public record SubmitMonthlyFeedbackRequest(
    [Required] Guid TeamMemberId,
    string? MonthYear = null,
    string? ScrumMasterFeedback = null,
    string? CdlFeedback = null,
    string? ClientFeedback = null,
    string? SelfReflection = null,
    int SmRating = 5,
    int HappinessIndex = 5,
    string? ActionItems = null,
    string? NextMonthGoals = null
);
