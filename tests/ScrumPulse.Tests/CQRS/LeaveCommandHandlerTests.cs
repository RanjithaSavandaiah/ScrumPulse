namespace ScrumPulse.Tests.CQRS;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.CQRS.Leaves;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class LeaveCommandHandlerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetLeavesQueryHandler_FiltersByMemberAndDates()
    {
        using var db = CreateInMemoryDbContext();
        var member1 = new TeamMember { Name = "Alice", Role = RoleType.Developer };
        var member2 = new TeamMember { Name = "Bob", Role = RoleType.Developer };
        db.TeamMembers.AddRange(member1, member2);

        db.TeamLeaves.AddRange(
            new TeamLeave { TeamMember = member1, StartDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc), Reason = "Vacation" },
            new TeamLeave { TeamMember = member2, StartDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc), Reason = "Conference" }
        );
        await db.SaveChangesAsync();

        var handler = new GetLeavesQueryHandler(db);
        var result = (await handler.HandleAsync(new GetLeavesQuery(MemberId: member1.Id))).ToList();

        Assert.Single(result);
        Assert.Equal("Alice", result[0].TeamMemberName);
    }

    [Fact]
    public async Task SubmitLeaveCommandHandler_Success_CreatesLeave()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Charlie", Role = RoleType.Developer };
        db.TeamMembers.Add(member);
        await db.SaveChangesAsync();

        var handler = new SubmitLeaveCommandHandler(db);
        var request = new SubmitLeaveRequest(
            TeamMemberId: member.Id,
            StartDate: new DateTime(2026, 8, 10),
            EndDate: new DateTime(2026, 8, 12),
            Reason: "Medical",
            LeaveType: "Sick Leave",
            Location: "Remote",
            LeaveSlot: "FullDay",
            CreatedBy: "Charlie"
        );

        var result = await handler.HandleAsync(new SubmitLeaveCommand(request, "Charlie"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Charlie", result.Value.TeamMemberName);
        Assert.Equal("Sick Leave", result.Value.LeaveType);
    }

    [Fact]
    public async Task SubmitLeaveCommandHandler_EmptyMember_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new SubmitLeaveCommandHandler(db);
        var request = new SubmitLeaveRequest(
            Guid.Empty,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1),
            "PrivilegeLeave",
            "Off",
            "FullDay",
            "Offshore",
            "Dev"
        );

        var result = await handler.HandleAsync(new SubmitLeaveCommand(request));

        Assert.True(result.IsFailure);
        Assert.Equal("BAD_REQUEST", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateLeaveCommandHandler_Success_UpdatesLeave()
    {
        using var db = CreateInMemoryDbContext();
        var member = new TeamMember { Name = "Dana", Role = RoleType.Developer };
        var leave = new TeamLeave
        {
            TeamMember = member,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1),
            Reason = "Old Reason"
        };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync();

        var handler = new UpdateLeaveCommandHandler(db);
        var updateRequest = new SubmitLeaveRequest(
            TeamMemberId: member.Id,
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(2),
            Reason: "Updated Reason",
            LeaveType: "Comp Off",
            Location: "Onsite",
            LeaveSlot: "HalfDayMorning",
            CreatedBy: "Lead"
        );

        var result = await handler.HandleAsync(new UpdateLeaveCommand(leave.Id, updateRequest, "Lead"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Updated Reason", result.Value.Reason);
    }

    [Fact]
    public async Task DeleteLeaveCommandHandler_Success_RemovesLeave()
    {
        using var db = CreateInMemoryDbContext();
        var leave = new TeamLeave { StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(1) };
        db.TeamLeaves.Add(leave);
        await db.SaveChangesAsync();

        var handler = new DeleteLeaveCommandHandler(db);
        var success = await handler.HandleAsync(new DeleteLeaveCommand(leave.Id));

        Assert.True(success);
        Assert.Empty(await db.TeamLeaves.ToListAsync());
    }
}
