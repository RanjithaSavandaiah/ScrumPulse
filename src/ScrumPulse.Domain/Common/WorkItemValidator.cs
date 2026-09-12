namespace ScrumPulse.Domain.Common;

using ScrumPulse.Domain.Enums;

/// <summary>
/// Domain-level validation rules for work items.
/// Eliminates duplicate validation logic across Create and Update operations (DRY principle).
/// </summary>
public static class WorkItemValidator
{
    private const string AcceptanceCriteriaMarker = "**Acceptance Criteria (DoR):**";

    /// <summary>
    /// Validates that a user story contains non-empty acceptance criteria.
    /// Returns a <see cref="Result"/> indicating success or failure with a descriptive error.
    /// </summary>
    public static Result ValidateAcceptanceCriteria(WorkItemType type, string? description)
    {
        if (type != WorkItemType.UserStory)
            return Result.Success();

        var idx = description?.IndexOf(AcceptanceCriteriaMarker, StringComparison.OrdinalIgnoreCase) ?? -1;
        if (idx >= 0)
        {
            var acContent = description![(idx + AcceptanceCriteriaMarker.Length)..].Trim();
            if (!string.IsNullOrWhiteSpace(acContent))
                return Result.Success();
        }

        return Result.Failure("Acceptance criteria is mandatory to add user story", "MISSING_ACCEPTANCE_CRITERIA");
    }

    /// <summary>
    /// Validates that a work item title is provided and non-empty.
    /// </summary>
    public static Result ValidateTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? Result.Failure("Work item title is mandatory", "MISSING_TITLE")
            : Result.Success();
    }

    /// <summary>
    /// Runs all validation rules for a work item and returns the first failure, or Success.
    /// </summary>
    public static Result Validate(string? title, WorkItemType type, string? description) =>
        Result.Combine(ValidateTitle(title), ValidateAcceptanceCriteria(type, description));
}
