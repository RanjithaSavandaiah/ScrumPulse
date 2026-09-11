namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record QualityGateCriterionDto(
    string Id,
    [Required][StringLength(200, MinimumLength = 2)] string Label,
    [StringLength(500)] string? Description = null,
    bool IsRequired = true
);

public record TeamDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string JoinCode,
    bool IsActive,
    DateTime CreatedAtUtc,
    List<QualityGateCriterionDto>? DorCriteria = null,
    List<QualityGateCriterionDto>? DodCriteria = null
);

public record CreateTeamRequest(
    [Required][StringLength(100, MinimumLength = 2)] string Name,
    [StringLength(300)] string? Description = null,
    [StringLength(80)] string? Slug = null
);

public record JoinTeamRequest(
    [Required][StringLength(20)] string JoinCode
);

public record ConfigureTeamGatesRequest(
    [Required] List<QualityGateCriterionDto> DorCriteria,
    [Required] List<QualityGateCriterionDto> DodCriteria
);
