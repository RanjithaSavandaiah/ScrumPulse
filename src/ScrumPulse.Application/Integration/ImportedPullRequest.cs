namespace ScrumPulse.Application.Integration;

/// <summary>
/// A pull request and its review signals as read from an external Git provider.
/// </summary>
public sealed record ImportedPullRequest(
    string ExternalPrId,
    string PrNumber,
    string Title,
    string Url,
    string? AuthorIdentity,
    string? AuthorDisplayName,
    string? ReviewerIdentity,
    string? ReviewerDisplayName,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc,
    string Status,
    int TotalCommentsCount,
    int ActionableCommentsCount,
    string ReviewSummary,
    IReadOnlyList<ReviewComment> Comments
);
