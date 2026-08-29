namespace ScrumPulse.Domain.Enums;

/// <summary>
/// Status of a pull request code review.
/// </summary>
public enum ReviewStatusType
{
    InReview = 0,
    Approved = 1,
    ChangesRequested = 2,
    Merged = 3,
    Closed = 4
}
