namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

/// <summary>Request DTO for creating sprints — prevents mass assignment of raw Sprint entity.</summary>
public record CreateSprintRequest(
    [Required][StringLength(100, MinimumLength = 3)] string Name,
    [Required][StringLength(500)] string Goal,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    bool IsActive = true,
    [Range(0, 500)] int CommittedStoryPoints = 0,
    [Range(1, 10)] int ConfidenceScore = 8,
    [StringLength(500)] string? ConfidenceNotes = null,
    [Range(1.0, 24.0)] double DailyWorkingHours = 8.5
);

/// <summary>Request DTO for updating sprints.</summary>
public record UpdateSprintRequest(
    [Required][StringLength(100, MinimumLength = 3)] string Name,
    [Required][StringLength(500)] string Goal,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    [Range(0, 500)] int CommittedStoryPoints = 0,
    [Range(0, 500)] int DeliveredStoryPoints = 0,
    [Range(1, 10)] int ConfidenceScore = 8,
    [StringLength(500)] string? ConfidenceNotes = null,
    [Range(1.0, 24.0)] double DailyWorkingHours = 8.5
);

/// <summary>Request DTO for creating team members — prevents mass assignment.</summary>
public record CreateTeamMemberRequest(
    [Required][StringLength(100, MinimumLength = 2)] string Name,
    [Required][EmailAddress][StringLength(200)] string Email,
    RoleType Role = RoleType.Developer,
    [StringLength(50)] string Location = "Offshore",
    [StringLength(50)] string TimeZone = "Asia/Kolkata",
    [StringLength(5)] string? Avatar = null,
    [Range(1, 10)] int ActiveWipLimit = 3,
    Guid? TeamId = null
);

/// <summary>Request DTO for updating team members.</summary>
public record UpdateTeamMemberRequest(
    [Required][StringLength(100, MinimumLength = 2)] string Name,
    [Required][EmailAddress][StringLength(200)] string Email,
    RoleType Role,
    [StringLength(50)] string Location = "Offshore",
    [StringLength(50)] string TimeZone = "Asia/Kolkata",
    [StringLength(5)] string? Avatar = null,
    [Range(1, 10)] int ActiveWipLimit = 3,
    Guid? TeamId = null
);

/// <summary>Request DTO for assigning/reassigning a member to a squad.</summary>
public record AssignMemberSquadRequest(Guid? TeamId);

/// <summary>Response DTO for Sprint — prevents leaking raw domain entities (RowVersion, DomainEvents, etc).</summary>
public record SprintDto(
    Guid Id,
    string Name,
    string Goal,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    int CommittedStoryPoints,
    int DeliveredStoryPoints,
    int ConfidenceScore,
    string? ConfidenceNotes,
    double DailyWorkingHours,
    Guid? TeamId
);
