namespace ScrumPulse.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

/// <summary>
/// Covers the global query filter that scopes every entity to the requesting squad.
/// </summary>
/// <remarks>
/// <para>
/// This is the guarantee the whole multi-squad design rests on, and it had no direct test.
/// Isolation previously depended on every handler remembering a Where clause and most did
/// not, so any caller could read another squad's standups and one-to-one feedback. Moving it
/// into the model means a forgotten filter is no longer a data breach - but only while these
/// tests hold.
/// </para>
/// <para>
/// Written against the behaviour rather than the implementation: each one asks "can this
/// context see data it should not", which stays meaningful if the filter is ever rewritten.
/// </para>
/// </remarks>
public class TenantIsolationTests
{
    private static readonly Guid SquadA = Guid.NewGuid();
    private static readonly Guid SquadB = Guid.NewGuid();

    /// <summary>
    /// Shared in-memory database, so two contexts scoped to different squads read the same
    /// rows. Separate databases would make every test pass regardless of the filter.
    /// </summary>
    private readonly string _databaseName = $"tenant-isolation-{Guid.NewGuid()}";

    private AppDbContext CreateContext(Guid? currentTeamId) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options,
            new TenantContext { CurrentTeamId = currentTeamId, CurrentUser = "Test" });

    /// <summary>Seeds one standup and one work item into each squad.</summary>
    private void SeedBothSquads()
    {
        foreach (var teamId in new[] { SquadA, SquadB })
        {
            using var context = CreateContext(teamId);

            context.TeamMembers.Add(new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = $"Member of {teamId}",
                Role = RoleType.Developer
            });

            context.DailyStandups.Add(new DailyStandup
            {
                Id = Guid.NewGuid(),
                TeamMemberId = Guid.NewGuid(),
                YesterdaySummary = $"Secret work for {teamId}",
                TodayPlan = "More of it"
            });

            context.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                Key = $"SP-{teamId.ToString()[..4]}",
                Title = "Confidential",
                Status = WorkItemStatus.InProgress
            });

            context.SaveChanges();
        }
    }

    [Fact]
    public async Task Queries_ReturnOnlyTheCurrentSquadsRows()
    {
        SeedBothSquads();

        await using var squadAContext = CreateContext(SquadA);
        var standups = await squadAContext.DailyStandups.ToListAsync();

        Assert.Single(standups);
        Assert.Contains(SquadA.ToString(), standups[0].YesterdaySummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Queries_CannotReachAnotherSquadByIdentifier()
    {
        // An attacker with a valid session for one squad guessing another squad's row id is
        // the IDOR case. The filter has to make the row invisible, not merely unlisted.
        SeedBothSquads();

        Guid otherSquadStandupId;
        await using (var squadBContext = CreateContext(SquadB))
        {
            otherSquadStandupId = (await squadBContext.DailyStandups.FirstAsync()).Id;
        }

        await using var squadAContext = CreateContext(SquadA);
        var stolen = await squadAContext.DailyStandups
            .FirstOrDefaultAsync(standup => standup.Id == otherSquadStandupId);

        Assert.Null(stolen);
    }

    [Fact]
    public async Task Counts_ExcludeOtherSquads()
    {
        // Aggregates leak just as effectively as rows: a count tells an attacker how much
        // another squad is delivering even when the detail is hidden.
        SeedBothSquads();

        await using var squadAContext = CreateContext(SquadA);

        Assert.Equal(1, await squadAContext.WorkItems.CountAsync());
        Assert.Equal(1, await squadAContext.TeamMembers.CountAsync());
    }

    [Fact]
    public async Task AnonymousContext_SeesNothing()
    {
        // The fail-closed case. A request with no session must read an empty dataset rather
        // than everything, which is what a null team id compared against null would have done.
        SeedBothSquads();

        await using var anonymousContext = CreateContext(currentTeamId: null);

        Assert.Empty(await anonymousContext.DailyStandups.ToListAsync());
        Assert.Empty(await anonymousContext.WorkItems.ToListAsync());
        Assert.Empty(await anonymousContext.TeamMembers.ToListAsync());
    }

    [Fact]
    public async Task Inserts_AreStampedWithTheCurrentSquad()
    {
        await using (var squadAContext = CreateContext(SquadA))
        {
            squadAContext.WorkItems.Add(new WorkItem { Id = Guid.NewGuid(), Key = "SP-1", Title = "New" });
            await squadAContext.SaveChangesAsync();
        }

        await using var squadBContext = CreateContext(SquadB);
        Assert.Empty(await squadBContext.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task Inserts_IgnoreATeamIdSuppliedByTheCaller()
    {
        // Mass assignment: a payload carrying another squad's TeamId must not plant a row
        // there. Ownership is taken from the session, overwriting whatever arrived.
        await using (var squadAContext = CreateContext(SquadA))
        {
            squadAContext.WorkItems.Add(new WorkItem
            {
                Id = Guid.NewGuid(),
                Key = "SP-2",
                Title = "Planted",
                TeamId = SquadB
            });

            await squadAContext.SaveChangesAsync();
        }

        await using var squadBContext = CreateContext(SquadB);
        Assert.Empty(await squadBContext.WorkItems.ToListAsync());

        await using var squadAVerification = CreateContext(SquadA);
        Assert.Single(await squadAVerification.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task Updates_CannotMoveARowToAnotherSquad()
    {
        // Ownership is immutable after insert, so a compromised update cannot migrate data
        // between squads.
        Guid workItemId;

        await using (var squadAContext = CreateContext(SquadA))
        {
            var workItem = new WorkItem { Id = Guid.NewGuid(), Key = "SP-3", Title = "Stays put" };
            squadAContext.WorkItems.Add(workItem);
            await squadAContext.SaveChangesAsync();
            workItemId = workItem.Id;
        }

        await using (var squadAContext = CreateContext(SquadA))
        {
            var workItem = await squadAContext.WorkItems.FirstAsync(item => item.Id == workItemId);
            workItem.TeamId = SquadB;
            await squadAContext.SaveChangesAsync();
        }

        await using var squadBContext = CreateContext(SquadB);
        Assert.Empty(await squadBContext.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task SoftDeletedRows_StayHidden()
    {
        Guid workItemId;

        await using (var context = CreateContext(SquadA))
        {
            var workItem = new WorkItem { Id = Guid.NewGuid(), Key = "SP-4", Title = "Removed" };
            context.WorkItems.Add(workItem);
            await context.SaveChangesAsync();
            workItemId = workItem.Id;
        }

        await using (var context = CreateContext(SquadA))
        {
            var workItem = await context.WorkItems.FirstAsync(item => item.Id == workItemId);
            workItem.IsDeleted = true;
            await context.SaveChangesAsync();
        }

        await using var verification = CreateContext(SquadA);
        Assert.Empty(await verification.WorkItems.ToListAsync());
    }

    [Fact]
    public async Task Teams_RemainVisible_SoThatJoiningBySecretCodeWorks()
    {
        // A team is identified by its own id rather than a TeamId pointing elsewhere, and
        // joining a squad must find one the caller is not yet scoped to. Access to teams is
        // gated by the join code in the join handler, not by this filter.
        var teamId = Guid.NewGuid();

        await using (var context = CreateContext(SquadA))
        {
            context.Teams.Add(new Team
            {
                Id = teamId,
                Name = "Platform Squad",
                Slug = "platform",
                JoinCode = "SQUAD-42",
                IsActive = true
            });

            await context.SaveChangesAsync();
        }

        await using var anonymousContext = CreateContext(currentTeamId: null);
        var found = await anonymousContext.Teams.FirstOrDefaultAsync(team => team.JoinCode == "SQUAD-42");

        Assert.NotNull(found);
    }
}
