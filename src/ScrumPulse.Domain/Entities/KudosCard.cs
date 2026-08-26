namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

public class KudosCard : BaseEntity
{
    public Guid SenderId { get; set; }
    public TeamMember? Sender { get; set; }

    public Guid ReceiverId { get; set; }
    public TeamMember? Receiver { get; set; }

    public BadgeType Badge { get; set; } = BadgeType.ProblemSolver;
    public string Message { get; set; } = string.Empty;
    public string ReactionEmojisJson { get; set; } = "{}";
}
