namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;

/// <summary>Typed response DTO for tech talk logs replaces anonymous object returns.</summary>
public record TechTalkLogDto(
    Guid Id,
    string Topic,
    Guid PresenterId,
    string? PresenterName,
    DateTime TalkDate,
    int DurationMinutes,
    string? KeyTakeaways,
    string? SlidesUrl
);

/// <summary>Request DTO for creating tech talk logs prevents mass assignment.</summary>
public record CreateTechTalkRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Topic,
    [Required] Guid PresenterId,
    DateTime? TalkDate = null,
    [Range(5, 480)] int DurationMinutes = 30,
    [StringLength(2000)] string? KeyTakeaways = null,
    [StringLength(500)] string? SlidesUrl = null
);

/// <summary>Request DTO for updating tech talk logs.</summary>
public record UpdateTechTalkRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Topic,
    [Required] Guid PresenterId,
    DateTime TalkDate,
    [Range(5, 480)] int DurationMinutes,
    [StringLength(2000)] string? KeyTakeaways = null,
    [StringLength(500)] string? SlidesUrl = null
);
