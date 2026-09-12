namespace ScrumPulse.Tests.CQRS;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.CQRS.Standups;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class StandupCommandHandlerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetStandupsQueryHandler_FiltersBySprintAndMember()
    {
        using var db = CreateInMemoryDbContext();
        var sprint1 = new Sprint { Name = "Sprint 1", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) };
        var sprint2 = new Sprint { Name = "Sprint 2", StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(14) };
        db.Sprints.AddRange(sprint1, sprint2);

        var member1 = new TeamMember { Name = "Eve", Role = RoleType.Developer };
        var member2 = new TeamMember { Name = "Frank", Role = RoleType.Developer };
        db.TeamMembers.AddRange(member1, member2);

        db.DailyStandups.AddRange(
            new DailyStandup { SprintId = sprint1.Id, TeamMember = member1, YesterdaySummary = "Y1", TodayPlan = "T1", StandupDate = DateTime.UtcNow },
            new DailyStandup { SprintId = sprint2.Id, TeamMember = member2, YesterdaySummary = "Y2", TodayPlan = "T2", StandupDate = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        var handler = new GetStandupsQueryHandler(db);
        var result = (await handler.HandleAsync(new GetStandupsQuery(SprintId: sprint1.Id))).ToList();

        Assert.Single(result);
        Assert.Equal("Eve", result[0].TeamMemberName);
    }

    [Fact]
    public async Task SubmitStandupCommandHandler_Success_CreatesStandup()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Grace", Role = RoleType.Developer };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var handler = new SubmitStandupCommandHandler(db);
        var request = new SubmitStandupRequest(
            member.Id,
            "Finished API endpoint",
            "Writing unit tests",
            "None",
            9,
            null
        );

        var result = await handler.HandleAsync(new SubmitStandupCommand(request));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Grace", result.Value.TeamMemberName);
        Assert.Equal("Finished API endpoint", result.Value.YesterdaySummary);
    }

    [Fact]
    public async Task SubmitStandupCommandHandler_MissingFields_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new SubmitStandupCommandHandler(db);

        // Missing member
        var res1 = await handler.HandleAsync(new SubmitStandupCommand(new SubmitStandupRequest(Guid.Empty, "Y", "T", null, 5, null)));
        Assert.True(res1.IsFailure);
        Assert.Equal("BAD_REQUEST", res1.ErrorCode);

        // Missing yesterday
        var res2 = await handler.HandleAsync(new SubmitStandupCommand(new SubmitStandupRequest(Guid.NewGuid(), "", "T", null, 5, null)));
        Assert.True(res2.IsFailure);
        Assert.Equal("BAD_REQUEST", res2.ErrorCode);

        // Missing today
        var res3 = await handler.HandleAsync(new SubmitStandupCommand(new SubmitStandupRequest(Guid.NewGuid(), "Y", " ", null, 5, null)));
        Assert.True(res3.IsFailure);
        Assert.Equal("BAD_REQUEST", res3.ErrorCode);
    }

    [Fact]
    public async Task UpdateStandupCommandHandler_Success_UpdatesFields()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Heidi", Role = RoleType.Developer };
        var standup = new DailyStandup { TeamMember = member, YesterdaySummary = "Old Y", TodayPlan = "Old T", StandupDate = DateTime.UtcNow };
        db.DailyStandups.Add(standup);
        await db.SaveChangesAsync();

        var handler = new UpdateStandupCommandHandler(db);
        var updateRequest = new SubmitStandupRequest(member.Id, "New Y", "New T", "Blocked on PR", 7, null);

        var result = await handler.HandleAsync(new UpdateStandupCommand(standup.Id, updateRequest));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("New Y", result.Value.YesterdaySummary);
        Assert.Equal("Blocked on PR", result.Value.BlockersText);
    }

    [Fact]
    public async Task DeleteStandupCommandHandler_Success_DeletesEntity()
    {
        using var db = CreateInMemoryDbContext();
        var standup = new DailyStandup { YesterdaySummary = "Y", TodayPlan = "T", StandupDate = DateTime.UtcNow };
        db.DailyStandups.Add(standup);
        await db.SaveChangesAsync();

        var handler = new DeleteStandupCommandHandler(db);
        var success = await handler.HandleAsync(new DeleteStandupCommand(standup.Id));

        Assert.True(success);
        Assert.Empty(await db.DailyStandups.ToListAsync());
    }
}
