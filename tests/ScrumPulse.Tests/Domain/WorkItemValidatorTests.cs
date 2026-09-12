namespace ScrumPulse.Tests.Domain;

using ScrumPulse.Domain.Common;
using ScrumPulse.Domain.Enums;
using Xunit;

public class WorkItemValidatorTests
{
    [Fact]
    public void Validate_WithValidTitle_ReturnsSuccess()
    {
        var result = WorkItemValidator.ValidateTitle("Implement login page");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyTitle_ReturnsFailure(string? title)
    {
        var result = WorkItemValidator.ValidateTitle(title);

        Assert.True(result.IsFailure);
        Assert.Equal("MISSING_TITLE", result.ErrorCode);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForTask_AlwaysSucceeds()
    {
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.TaskPbi, null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForBug_AlwaysSucceeds()
    {
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.Bug, "Some bug description");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForUserStory_WithCriteria_Succeeds()
    {
        var description = "As a user I want to login\n**Acceptance Criteria (DoR):**\n- Given valid credentials, when I click login, then I should see the dashboard";
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.UserStory, description);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForUserStory_WithoutCriteria_ReturnsFailure()
    {
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.UserStory, "Just a description without AC");

        Assert.True(result.IsFailure);
        Assert.Equal("MISSING_ACCEPTANCE_CRITERIA", result.ErrorCode);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForUserStory_WithEmptyMarker_ReturnsFailure()
    {
        var description = "Description\n**Acceptance Criteria (DoR):**\n   ";
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.UserStory, description);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_ForUserStory_WithNullDescription_ReturnsFailure()
    {
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.UserStory, null);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Validate_CombinesAllRules_ReturnsFirstFailure()
    {
        // Empty title fails first
        var result = WorkItemValidator.Validate("", WorkItemType.UserStory, "No AC either");

        Assert.True(result.IsFailure);
        Assert.Contains("title", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithValidTitleAndMissingAC_ReturnsACFailure()
    {
        var result = WorkItemValidator.Validate("Valid Title", WorkItemType.UserStory, "No acceptance criteria");

        Assert.True(result.IsFailure);
        Assert.Contains("Acceptance criteria", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithValidTaskAndNoAC_Succeeds()
    {
        var result = WorkItemValidator.Validate("Fix login bug", WorkItemType.TaskPbi, "Just fix it");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateAcceptanceCriteria_CaseInsensitiveMarkerMatch()
    {
        var description = "Story\n**acceptance criteria (dor):**\n- criterion 1";
        var result = WorkItemValidator.ValidateAcceptanceCriteria(WorkItemType.UserStory, description);

        Assert.True(result.IsSuccess);
    }
}
