namespace ScrumPulse.Tests.CQRS;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.CQRS.Teams;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class TeamCommandHandlerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetTeamsQueryHandler_ReturnsOnlyActiveTeams()
    {
        using var db = CreateInMemoryDbContext();
        db.Teams.AddRange(
            new Team { Name = "Alpha Squad", Slug = "alpha-squad", JoinCode = "ALPH01", IsActive = true },
            new Team { Name = "Beta Squad", Slug = "beta-squad", JoinCode = "BETA01", IsActive = true },
            new Team { Name = "Archived Squad", Slug = "archived-squad", JoinCode = "ARCH01", IsActive = false }
        );
        await db.SaveChangesAsync();

        var handler = new GetTeamsQueryHandler(db);
        var result = (await handler.HandleAsync(new GetTeamsQuery())).ToList();

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "Alpha Squad");
        Assert.Contains(result, t => t.Name == "Beta Squad");
        Assert.DoesNotContain(result, t => t.Name == "Archived Squad");
    }

    [Fact]
    public async Task GetTeamByIdQueryHandler_ReturnsTeam_WhenExists()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team { Name = "Falcon Squad", Slug = "falcon-squad", JoinCode = "FALC01", IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new GetTeamByIdQueryHandler(db);
        var result = await handler.HandleAsync(new GetTeamByIdQuery(team.Id));

        Assert.NotNull(result);
        Assert.Equal("Falcon Squad", result.Name);
    }

    [Fact]
    public async Task GetTeamByIdQueryHandler_ReturnsNull_WhenNotFound()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new GetTeamByIdQueryHandler(db);
        var result = await handler.HandleAsync(new GetTeamByIdQuery(Guid.NewGuid()));

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateTeamCommandHandler_Success_GeneratesSlugAndJoinCode()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new CreateTeamCommandHandler(db);

        var request = new CreateTeamRequest("Titan Squad", "Heavy engineering squad");
        var result = await handler.HandleAsync(new CreateTeamCommand(request));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Titan Squad", result.Value.Name);
        Assert.Equal("titan-squad", result.Value.Slug);
        Assert.Equal(6, result.Value.JoinCode.Length);
        Assert.True(result.Value.IsActive);
    }

    [Fact]
    public async Task CreateTeamCommandHandler_DuplicateName_ReturnsConflict()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new CreateTeamCommandHandler(db);

        await handler.HandleAsync(new CreateTeamCommand(new CreateTeamRequest("Omega Squad", "First squad")));
        var duplicateResult = await handler.HandleAsync(new CreateTeamCommand(new CreateTeamRequest("  omega squad  ", "Duplicate")));

        Assert.True(duplicateResult.IsFailure);
        Assert.Equal("CONFLICT", duplicateResult.ErrorCode);
        Assert.Contains("already exists", duplicateResult.Error);
    }

    [Fact]
    public async Task CreateTeamCommandHandler_DuplicateSlug_AppendsNumericSuffix()
    {
        using var db = CreateInMemoryDbContext();
        db.Teams.Add(new Team { Name = "Apex Core", Slug = "apex", JoinCode = "APEX01", IsActive = true });
        await db.SaveChangesAsync();

        var handler = new CreateTeamCommandHandler(db);
        var result = await handler.HandleAsync(new CreateTeamCommand(new CreateTeamRequest("Apex Advanced", "Second apex", "apex")));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.StartsWith("apex-", result.Value.Slug);
    }

    [Fact]
    public async Task JoinTeamCommandHandler_Success_ReturnsTeam()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team { Name = "Delta Squad", Slug = "delta-squad", JoinCode = "DELT01", IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new JoinTeamCommandHandler(db);
        var result = await handler.HandleAsync(new JoinTeamCommand(new JoinTeamRequest("delt01")));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(team.Id, result.Value.Id);
    }

    [Fact]
    public async Task JoinTeamCommandHandler_NotFound_ReturnsFailure()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new JoinTeamCommandHandler(db);
        var result = await handler.HandleAsync(new JoinTeamCommand(new JoinTeamRequest("UNKNOWN")));

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateTeamCommandHandler_Success_UpdatesNameAndDescription()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team { Name = "Old Name", Description = "Old Desc", Slug = "old-name", JoinCode = "OLD001", IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new UpdateTeamCommandHandler(db);
        var result = await handler.HandleAsync(new UpdateTeamCommand(team.Id, new CreateTeamRequest("New Name", "New Desc")));

        Assert.NotNull(result);
        Assert.Equal("New Name", result.Name);
        Assert.Equal("New Desc", result.Description);
    }

    [Fact]
    public async Task UpdateTeamCommandHandler_NotFound_ReturnsNull()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new UpdateTeamCommandHandler(db);
        var result = await handler.HandleAsync(new UpdateTeamCommand(Guid.NewGuid(), new CreateTeamRequest("Name", "Desc")));

        Assert.Null(result);
    }

    [Fact]
    public async Task ConfigureQualityGatesCommandHandler_Success_UpdatesCriteria()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team { Name = "Gate Squad", Slug = "gate-squad", JoinCode = "GATE01", IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new ConfigureQualityGatesCommandHandler(db);
        var dor = new List<QualityGateCriterionDto> { new("dor-1", "Design ready", null, true) };
        var dod = new List<QualityGateCriterionDto> { new("dod-1", "CI green", null, true) };

        var result = await handler.HandleAsync(new ConfigureQualityGatesCommand(team.Id, new ConfigureTeamGatesRequest(dor, dod)));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.DorCriteria);
        Assert.Single(result.Value.DorCriteria);
        Assert.Equal("Design ready", result.Value.DorCriteria[0].Label);
    }

    [Fact]
    public async Task ConfigureQualityGatesCommandHandler_EmptyDor_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new ConfigureQualityGatesCommandHandler(db);
        var dod = new List<QualityGateCriterionDto> { new("dod-1", "CI green", null, true) };

        var result = await handler.HandleAsync(new ConfigureQualityGatesCommand(Guid.NewGuid(), new ConfigureTeamGatesRequest(new List<QualityGateCriterionDto>(), dod)));

        Assert.True(result.IsFailure);
        Assert.Equal("BAD_REQUEST", result.ErrorCode);
    }

    [Fact]
    public async Task ConfigureQualityGatesCommandHandler_EmptyDod_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new ConfigureQualityGatesCommandHandler(db);
        var dor = new List<QualityGateCriterionDto> { new("dor-1", "Design ready", null, true) };

        var result = await handler.HandleAsync(new ConfigureQualityGatesCommand(Guid.NewGuid(), new ConfigureTeamGatesRequest(dor, new List<QualityGateCriterionDto>())));

        Assert.True(result.IsFailure);
        Assert.Equal("BAD_REQUEST", result.ErrorCode);
    }

    [Fact]
    public async Task ConfigureQualityGatesCommandHandler_NotFound_ReturnsNotFound()
    {
        using var db = CreateInMemoryDbContext();
        var handler = new ConfigureQualityGatesCommandHandler(db);
        var dor = new List<QualityGateCriterionDto> { new("dor-1", "Design ready", null, true) };
        var dod = new List<QualityGateCriterionDto> { new("dod-1", "CI green", null, true) };

        var result = await handler.HandleAsync(new ConfigureQualityGatesCommand(Guid.NewGuid(), new ConfigureTeamGatesRequest(dor, dod)));

        Assert.True(result.IsFailure);
        Assert.Equal("NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetQualityGatesQueryHandler_ReturnsDefaultCriteria_WhenNoneConfigured()
    {
        using var db = CreateInMemoryDbContext();
        var team = new Team { Name = "Default Squad", Slug = "default-squad", JoinCode = "DEFT01", IsActive = true };
        db.Teams.Add(team);
        await db.SaveChangesAsync();

        var handler = new GetQualityGatesQueryHandler(db);
        var result = await handler.HandleAsync(new GetQualityGatesQuery(team.Id));

        Assert.NotNull(result);
        Assert.NotEmpty(result.DorCriteria);
        Assert.NotEmpty(result.DodCriteria);
    }
}
