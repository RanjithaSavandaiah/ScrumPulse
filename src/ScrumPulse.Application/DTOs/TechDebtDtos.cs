namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

/// <summary>Typed response DTO for tech debt items — replaces anonymous object returns.</summary>
public record TechDebtItemDto(
    Guid Id,
    string Title,
    string Description,
    TechDebtSeverity Severity,
    int EstimatedHours,
    TechDebtStatus Status,
    Guid? PayoffSprintId,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime CreatedAtUtc
);

/// <summary>Request DTO for creating/updating tech debt items — prevents mass assignment.</summary>
public record CreateTechDebtRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    [Required][StringLength(2000)] string Description,
    TechDebtSeverity Severity = TechDebtSeverity.Medium,
    [Range(1, 1000)] int EstimatedHours = 8,
    Guid? PayoffSprintId = null,
    Guid? AssigneeId = null
);

/// <summary>Request DTO for updating tech debt items.</summary>
public record UpdateTechDebtRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    [Required][StringLength(2000)] string Description,
    TechDebtSeverity Severity,
    [Range(1, 1000)] int EstimatedHours,
    TechDebtStatus Status,
    Guid? PayoffSprintId = null,
    Guid? AssigneeId = null
);

/// <summary>Request DTO for resolving tech debt.</summary>
public record ResolveTechDebtRequest(TechDebtStatus Status = TechDebtStatus.Resolved);
