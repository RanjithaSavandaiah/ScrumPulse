namespace ScrumPulse.Tests.AI;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Providers;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

/// <summary>
/// Covers the standup scribe's behaviour when no model is available.
/// </summary>
/// <remarks>
/// <para>
/// Tests never reach a real provider: that would need an API key, spend the daily free-tier
/// allowance, and return different text on each run. These exercise the path a deployment
/// takes once the allowance is gone or no key is configured, which is the behaviour that has
/// to stay correct without anyone noticing it has engaged.
/// </para>
/// <para>
/// The guarantee being protected is that nothing is written unless it can be attributed to a
/// real roster member. A transcript filed under the wrong colleague is worse than one not
/// filed at all, because a reviewer reads it as that person's own words.
/// </para>
/// </remarks>
public class StandupTranscriptServiceTests
{
    private const string Transcript =
        "Priya here. Yesterday I raised the PR for partial refunds. Today I am chasing the client answer.";

    /// <summary>Provider reporting no inference, as when no API key is configured.</summary>
    private sealed class OfflineAgentProvider : IAgentProvider
    {
        public bool IsLiveInferenceAvailable => false;

        public IReadOnlyList<ProvisionedAgent> BuildAgentChain(
            string instructions,
            string agentName,
            IReadOnlyList<Microsoft.Extensions.AI.AITool>? tools = null) => [];
    }

    private static (StandupTranscriptService Service, AppDbContext DbContext, Guid TeamId) CreateService(
        bool seedRoster = true,
        DbContextOptions<AppDbContext>? existingOptions = null)
    {
        var teamId = Guid.NewGuid();

        var tenantContext = new TenantContext
        {
            CurrentTeamId = teamId,
            CurrentUser = "Ranjitha"
        };

        var options = existingOptions ?? new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"transcript-tests-{Guid.NewGuid()}")
            .Options;

        var dbContext = new AppDbContext(options, tenantContext);

        if (seedRoster)
        {
            dbContext.TeamMembers.AddRange(
                new TeamMember { Id = Guid.NewGuid(), Name = "Priya Desai", Role = RoleType.Developer, TeamId = teamId },
                new TeamMember { Id = Guid.NewGuid(), Name = "Tom Whitfield", Role = RoleType.Developer, TeamId = teamId });

            dbContext.SaveChanges();
        }

        var service = new StandupTranscriptService(
            dbContext,
            new OfflineAgentProvider(),
            new DailyRequestBudget(dailyLimit: 0, TimeProvider.System, NullLogger<DailyRequestBudget>.Instance),
            new AgentConfiguration(),
            tenantContext,
            NullLogger<StandupTranscriptService>.Instance);

        return (service, dbContext, teamId);
    }

    [Fact]
    public async Task DraftFromTranscript_WithoutInference_ReportsItWasNotModelGenerated()
    {
        // The UI uses this flag to tell the Scrum Master the transcript was not split by
        // speaker, so a silent false would leave them assuming attribution had happened.
        var (service, _, _) = CreateService();

        var result = await service.DraftFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.False(result.WasModelGenerated);
    }

    [Fact]
    public async Task DraftFromTranscript_WithoutInference_DoesNotGuessAnAttribution()
    {
        // "Priya here" appears verbatim in the transcript, so naive string matching would
        // attribute it. Guessing is exactly what must not happen without a model.
        var (service, _, _) = CreateService();

        var result = await service.DraftFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.All(result.Drafts, draft => Assert.Null(draft.TeamMemberId));
    }

    [Fact]
    public async Task DraftFromTranscript_WithoutInference_ReturnsTheTranscriptForManualEntry()
    {
        var (service, _, _) = CreateService();

        var result = await service.DraftFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        var draft = Assert.Single(result.Drafts);
        Assert.Contains("partial refunds", draft.YesterdaySummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftFromTranscript_WithEmptyRoster_ReturnsNothing()
    {
        // No roster means nobody to attribute to. Returning the raw transcript as a draft
        // would be an update belonging to no one.
        var (service, _, _) = CreateService(seedRoster: false);

        var result = await service.DraftFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.Empty(result.Drafts);
        Assert.False(result.WasModelGenerated);
    }

    [Fact]
    public async Task DraftFromTranscript_UsesNeutralMood_WhenNothingWasInferred()
    {
        // Zero would read as the lowest possible wellbeing score rather than "not measured",
        // and that figure feeds the happiness trend shown to leadership.
        var (service, _, _) = CreateService();

        var result = await service.DraftFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.All(result.Drafts, draft => Assert.InRange(draft.MoodScore, 1, 5));
    }

    [Fact]
    public async Task AutoLog_WritesNothing_WhenNoDraftCanBeAttributed()
    {
        // The guarantee that matters: the unattributed fallback must never reach the feed as
        // somebody's standup.
        var (service, dbContext, _) = CreateService();

        var result = await service.AutoLogFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.Equal(0, result.SavedCount);
        Assert.Empty(await dbContext.DailyStandups.ToListAsync());
    }

    [Fact]
    public async Task AutoLog_ReportsItWasNotModelGenerated_SoTheUiCanExplainTheEmptyResult()
    {
        var (service, _, _) = CreateService();

        var result = await service.AutoLogFromTranscriptAsync(
            new DraftStandupsFromTranscriptRequest(Transcript, null));

        Assert.False(result.WasModelGenerated);
        Assert.Empty(result.SavedMemberNames);
    }

    [Fact]
    public async Task AutoLog_HonoursCancellation()
    {
        var (service, _, _) = CreateService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AutoLogFromTranscriptAsync(
                new DraftStandupsFromTranscriptRequest(Transcript, null),
                cancellation.Token));
    }

    [Fact]
    public async Task DraftFromTranscript_OnlyConsidersTheRequestingSquadsRoster()
    {
        // The roster bounds who the scribe may attribute to. If it leaked across squads, a
        // name shared between two teams could file an update against the wrong person.
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"transcript-tests-{Guid.NewGuid()}")
            .Options;

        var (service, dbContext, teamId) = CreateService(existingOptions: dbOptions);

        var otherTeamId = Guid.NewGuid();
        using (var otherContext = new AppDbContext(dbOptions, new TenantContext { CurrentTeamId = otherTeamId, CurrentUser = "Other" }))
        {
            otherContext.TeamMembers.Add(new TeamMember
            {
                Id = Guid.NewGuid(),
                Name = "Someone Else",
                Role = RoleType.Developer
            });
            await otherContext.SaveChangesAsync();
        }

        var visibleRoster = await dbContext.TeamMembers.Select(member => member.Name).ToListAsync();

        Assert.DoesNotContain("Someone Else", visibleRoster);
        Assert.Equal(2, visibleRoster.Count);
    }
}
