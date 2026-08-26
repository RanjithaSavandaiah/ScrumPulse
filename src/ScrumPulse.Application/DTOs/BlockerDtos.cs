namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

public record BlockerDto(
    Guid Id,
    string Title,
    string Description,
    BlockerCategory Category,
    int SlaHoursLimit,
    Guid? WorkItemId,
    string? WorkItemKey,
    Guid? RaisedById,
    string? RaisedByName,
    Guid? SprintId,
    DateTime RaisedAtUtc,
    DateTime? ResolvedAtUtc,
    string? ResolutionNotes,
    bool IsResolved,
    double HoursWaiting,
    bool IsSlaBreached
);

public record CreateBlockerRequest(
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    [Required][StringLength(2000)] string Description,
    BlockerCategory Category,
    [Range(1, 168)] int SlaHoursLimit,
    Guid? WorkItemId,
    [Required] Guid RaisedById,
    Guid? SprintId
);

public record ResolveBlockerRequest([Required][StringLength(2000)] string ResolutionNotes);
