namespace ScrumPulse.Tests.Domain;

using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using Xunit;

public class DomainEntitiesTests
{
    [Fact]
    public void TeamMember_Initialization_SetsPropertiesProperly()
    {
        var member = new TeamMember
        {
            Name = "Priya Sharma",
            Email = "priya.sharma@scrumpulse.com",
            Role = RoleType.Developer,
            Location = "Bangalore Offshore",
            IsActive = true
        };

        Assert.Equal("Priya Sharma", member.Name);
        Assert.Equal(RoleType.Developer, member.Role);
        Assert.Equal("Bangalore Offshore", member.Location);
        Assert.True(member.IsActive);
    }

    [Fact]
    public void Sprint_WorkingDays_ComputesDurationCorrectly()
    {
        var start = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 14, 0, 0, 0, DateTimeKind.Utc);
        var sprint = new Sprint
        {
            Name = "Sprint 25",
            StartDate = start,
            EndDate = end,
            CommittedStoryPoints = 40,
            IsActive = true
        };

        Assert.Equal("Sprint 25", sprint.Name);
        Assert.Equal(13, (sprint.EndDate - sprint.StartDate).Days);
        Assert.True(sprint.IsActive);
    }

    [Fact]
    public void TeamLeave_DaysCalculation_WorksAcrossDates()
    {
        var start = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 8, 12, 0, 0, 0, DateTimeKind.Utc);
        var leave = new TeamLeave
        {
            StartDate = start,
            EndDate = end,
            Reason = "Festival PTO",
            LeaveType = "Planned PTO",
            IsApproved = true
        };

        Assert.Equal(3, leave.TotalDays);
        Assert.True(leave.IsApproved);
    }

    [Fact]
    public void KudosCard_Reactions_InitializesDictionaryProperly()
    {
        var kudos = new KudosCard
        {
            SenderId = Guid.NewGuid(),
            ReceiverId = Guid.NewGuid(),
            Badge = BadgeType.InnovationStar,
            Message = "Great job optimizing the build pipeline!"
        };

        Assert.Equal("{}", kudos.ReactionEmojisJson);
        Assert.Equal(BadgeType.InnovationStar, kudos.Badge);
        Assert.Equal("Great job optimizing the build pipeline!", kudos.Message);
    }

    [Fact]
    public void RetroCard_Upvotes_DefaultsToZero()
    {
        var retro = new RetroCard
        {
            Category = RetroCategory.WentWell,
            Content = "Smooth deployment on staging",
            IsAnonymous = false
        };

        Assert.Equal(0, retro.UpvotesCount);
        Assert.Equal("[]", retro.UpvoterMemberIdsJson);
    }

    [Fact]
    public void TechDebtItem_Defaults_AreSetProperly()
    {
        var techDebt = new TechDebtItem
        {
            Title = "Upgrade Npgsql driver",
            Description = "Upgrade to v10",
            Severity = "Medium",
            EstimatedHours = 4,
            Status = "Identified"
        };

        Assert.Equal("Upgrade Npgsql driver", techDebt.Title);
        Assert.Equal(4, techDebt.EstimatedHours);
        Assert.Equal("Identified", techDebt.Status);
    }

    [Fact]
    public void TechTalkLog_Properties_SetCorrectly()
    {
        var presenterId = Guid.NewGuid();
        var talk = new TechTalkLog
        {
            Topic = "Clean Architecture with EF Core",
            PresenterId = presenterId,
            DurationMinutes = 45,
            KeyTakeaways = "Segregate Domain and Persistence"
        };

        Assert.Equal("Clean Architecture with EF Core", talk.Topic);
        Assert.Equal(presenterId, talk.PresenterId);
        Assert.Equal(45, talk.DurationMinutes);
    }
}
