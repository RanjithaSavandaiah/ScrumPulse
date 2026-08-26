namespace ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Common;

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

    public int TotalCommentsCount { get; set; } = 0;
    public int ActionableCommentsCount { get; set; } = 0;
    public string ReviewSummary { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Approved"; // Approved, ChangesRequested, Merged, InReview

    public DateTime? MergedAtUtc { get; set; }
}
