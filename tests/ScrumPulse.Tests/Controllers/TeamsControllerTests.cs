namespace ScrumPulse.Tests.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Api.Controllers;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class TeamsControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyActiveTeams()
    {
        using var db = CreateInMemoryDbContext();
        db.Teams.AddRange(
            new Team { Name = "Squad Alpha", Slug = "squad-alpha", JoinCode = "ALPHA1", IsActive = true },
            new Team { Name = "Squad Beta", Slug = "squad-beta", JoinCode = "BETA22", IsActive = true },
            new Team { Name = "Archived Squad", Slug = "archived-squad", JoinCode = "ARCH99", IsActive = false }
        );
        await db.SaveChangesAsync();

        var controller = new TeamsController(db);
        var actionResult = await controller.GetAll();
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var teams = Assert.IsAssignableFrom<IEnumerable<TeamDto>>(okResult.Value).ToList();

        Assert.Equal(2, teams.Count);
        Assert.Contains(teams, t => t.Name == "Squad Alpha");
        Assert.Contains(teams, t => t.Name == "Squad Beta");
        Assert.DoesNotContain(teams, t => t.Name == "Archived Squad");
    }

    [Fact]
    public async Task Create_GeneratesSlugAndJoinCode()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new TeamsController(db);

        var request = new CreateTeamRequest("Phoenix Squad", "Platform engineering and core checkout squad");
        var actionResult = await controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var team = Assert.IsType<TeamDto>(createdResult.Value);

        Assert.Equal("Phoenix Squad", team.Name);
        Assert.Equal("phoenix-squad", team.Slug);
        Assert.Equal(6, team.JoinCode.Length);
        Assert.True(team.IsActive);

        var inDb = await db.Teams.FirstOrDefaultAsync(t => t.Id == team.Id);
        Assert.NotNull(inDb);
        Assert.Equal(team.JoinCode, inDb.JoinCode);
    }

    [Fact]
    public async Task Join_WithValidCode_ReturnsTeam()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team
        {
            Name = "Apollo Squad",
            Slug = "apollo-squad",
            JoinCode = "APOLL7",
            IsActive = true
        };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var controller = new TeamsController(db);
        var actionResult = await controller.Join(new JoinTeamRequest("apoll7")); // test case insensitivity
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var result = Assert.IsType<TeamDto>(okResult.Value);

        Assert.Equal("Apollo Squad", result.Name);
        Assert.Equal(team.Id, result.Id);
    }

    [Fact]
    public async Task Join_WithInvalidCode_ReturnsNotFound()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new TeamsController(db);

        var actionResult = await controller.Join(new JoinTeamRequest("NONEXIST"));
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task Create_WhenUserIsDeveloper_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new TeamsController(db)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };
        controller.ControllerContext.HttpContext.Request.Headers["X-User-Role"] = "Developer";

        var request = new CreateTeamRequest("Unauthorized Squad", "Should fail");
        var actionResult = await controller.Create(request);

        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
