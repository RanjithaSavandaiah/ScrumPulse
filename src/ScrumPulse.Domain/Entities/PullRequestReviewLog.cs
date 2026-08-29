namespace ScrumPulse.Domain.Entities;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;

/// <summary>
/// Records pull request review activity including comment metrics
/// for code review quality analysis.
/// </summary>
public class PullRequestReviewLog : BaseEntity
{
    public Guid? WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public Guid AuthorId { get; set; }
    public TeamMember? Author { get; set; }

    public Guid? ReviewerId { get; set; }
    public TeamMember? Reviewer { get; set; }

    public Guid? SprintId { get; set; }
    public Sprint? Sprint { get; set; }

    public string PrNumber { get; set; } = string.Empty;
    public string PrTitle { get; set; } = string.Empty;
    public string PrUrl { get; set; } = string.Empty;

    public int TotalCommentsCount { get; set; }
    public int ActionableCommentsCount { get; set; }
    public string ReviewSummary { get; set; } = string.Empty;
    public ReviewStatusType ReviewStatus { get; set; } = ReviewStatusType.Approved;

    public DateTime? MergedAtUtc { get; set; }
}
