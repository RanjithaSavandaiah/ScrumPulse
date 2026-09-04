namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

public record TeamDto(
    Guid Id,
    string Name,
    string Slug,
    string Description,
    string JoinCode,
    bool IsActive,
    DateTime CreatedAtUtc
);

public record CreateTeamRequest(
    [Required][StringLength(100, MinimumLength = 2)] string Name,
    [StringLength(300)] string? Description = null,
    [StringLength(80)] string? Slug = null
);

public record JoinTeamRequest(
    [Required][StringLength(20)] string JoinCode
);
