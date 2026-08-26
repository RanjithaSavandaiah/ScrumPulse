namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

public record RetroCardDto(
    Guid Id,
    Guid? SprintId,
    RetroCategory Category,
    string Content,
    Guid? AuthorId,
    string? AuthorName,
    bool IsAnonymous,
    int UpvotesCount,
    List<Guid> UpvoterMemberIds
);

public record CreateRetroCardRequest(
    Guid? SprintId,
    RetroCategory Category,
    [Required][StringLength(1000, MinimumLength = 2)] string Content,
    Guid? AuthorId,
    bool IsAnonymous
);

public record RetroActionItemDto(
    Guid Id,
    Guid? SprintId,
    string Title,
    Guid? AssigneeId,
    string? AssigneeName,
    DateTime? DueDate,
    bool IsCompleted
);

public record CreateRetroActionItemRequest(
    Guid? SprintId,
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    Guid? AssigneeId,
    DateTime? DueDate
);

public record UpdateRetroCardRequest(
    Guid? SprintId,
    RetroCategory Category,
    [Required][StringLength(1000, MinimumLength = 2)] string Content,
    Guid? AuthorId,
    bool IsAnonymous
);

public record UpdateRetroActionItemRequest(
    Guid? SprintId,
    [Required][StringLength(250, MinimumLength = 3)] string Title,
    Guid? AssigneeId,
    DateTime? DueDate,
    bool IsCompleted
);
