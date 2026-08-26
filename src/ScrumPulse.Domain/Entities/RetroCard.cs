namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

public class RetroCard : BaseEntity
{
    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public RetroCategory Category { get; set; } = RetroCategory.WentWell;
    public string Content { get; set; } = string.Empty;

    public Guid? AuthorId { get; set; }
    public TeamMember? Author { get; set; }

    public bool IsAnonymous { get; set; } = false;
    public int UpvotesCount { get; set; } = 0;
    public string UpvoterMemberIdsJson { get; set; } = "[]";
}
