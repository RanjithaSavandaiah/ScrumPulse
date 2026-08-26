namespace ScrumPulse.Application.DTOs;

using System.ComponentModel.DataAnnotations;
using ScrumPulse.Domain.Enums;

public record KudosDto(
    Guid Id,
    Guid SenderId,
    string SenderName,
    Guid ReceiverId,
    string ReceiverName,
    BadgeType Badge,
    string Message,
    Dictionary<string, int> ReactionEmojis,
    DateTime CreatedAtUtc
);

public record SendKudosRequest(
    [Required] Guid SenderId,
    [Required] Guid ReceiverId,
    BadgeType Badge,
    [Required][StringLength(1000, MinimumLength = 2)] string Message
);

public record ReactToKudosRequest(string? ReactionType = null, string? Emoji = null);
