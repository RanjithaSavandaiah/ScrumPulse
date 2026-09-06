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

    [Fact]
    public async Task Create_WhenDuplicateSquadNameExists_ReturnsConflict()
    {
        using var db = CreateInMemoryDbContext();
        var controller = new TeamsController(db);

        var firstRequest = new CreateTeamRequest("Titan Squad", "First squad description");
        var firstResult = await controller.Create(firstRequest);
        Assert.IsType<CreatedAtActionResult>(firstResult.Result);

        // Attempt duplicate squad name (with leading/trailing spaces and different casing)
        var duplicateRequest = new CreateTeamRequest("  titan squad  ", "Duplicate squad attempt");
        var duplicateResult = await controller.Create(duplicateRequest);

        var conflictResult = Assert.IsType<ConflictObjectResult>(duplicateResult.Result);
        Assert.Equal(Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict, conflictResult.StatusCode);

        // Ensure database only contains 1 team
        var teamsCount = await db.Teams.CountAsync(t => t.IsActive);
        Assert.Equal(1, teamsCount);
    }

    [Fact]
    public async Task Create_WithIdempotencyKey_ReturnsCachedResponseOnRepeat()
    {
        using var db = CreateInMemoryDbContext();
        var store = new ScrumPulse.Infrastructure.Services.MemoryIdempotencyStore();
        var controller = new TeamsController(db, store);

        var request = new CreateTeamRequest("Nexus Squad", "Core platform squad");
        const string idempotencyKey = "key-nexus-123";

        // First call creates team
        var firstResult = await controller.Create(request, idempotencyKey);
        var createdResult = Assert.IsType<CreatedAtActionResult>(firstResult.Result);
        var createdTeam = Assert.IsType<TeamDto>(createdResult.Value);

        // Second call with same idempotency key returns cached response immediately
        var repeatResult = await controller.Create(request, idempotencyKey);
        var okResult = Assert.IsType<OkObjectResult>(repeatResult.Result);
        var cachedTeam = Assert.IsType<TeamDto>(okResult.Value);

        Assert.Equal(createdTeam.Id, cachedTeam.Id);
        Assert.Equal("Nexus Squad", cachedTeam.Name);

        // Ensure only one squad was created in database
        var totalSquads = await db.Teams.CountAsync(t => t.IsActive);
        Assert.Equal(1, totalSquads);
    }
}
