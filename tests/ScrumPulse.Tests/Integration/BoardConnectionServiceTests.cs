namespace ScrumPulse.Tests.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Services;
using Xunit;

/// <summary>
/// Covers the service that owns a squad's board connection and both import routes.
/// </summary>
/// <remarks>
/// <para>
/// Everything expensive to get wrong in this feature lives here. A stored credential that can
/// read a client's backlog is written, kept and destroyed by this class; the decision about
/// whose work may be imported is applied here; and the upsert that either refreshes a hundred
/// items or duplicates them runs here. None of those failures announce themselves - a lost
/// credential surfaces as a sync that stopped working, a bad upsert as a velocity figure that
/// doubled.
/// </para>
/// <para>
/// Run against the real DbContext on an in-memory provider rather than a mocked one, because
/// several of these guarantees are properties of the tracked graph: that a second save updates
/// one row rather than adding another, that a soft delete leaves the row behind with its
/// credential cleared, that an upsert matches on the external id.
/// </para>
/// </remarks>
public class BoardConnectionServiceTests
{
    private static readonly Guid SquadId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Shared database name, so a context opened to verify a write sees what the service wrote.
    /// </summary>
    private readonly string _databaseName = $"board-connection-{Guid.NewGuid()}";

    private AppDbContext CreateContext() =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_databaseName).Options,
            new TenantContext { CurrentTeamId = SquadId, CurrentUser = "Test" });

    private BoardConnectionService CreateService(AppDbContext dbContext, FakeBoardSyncClient? syncClient = null) =>
        new(
            dbContext,
            syncClient ?? new FakeBoardSyncClient(),
            new PrefixCredentialProtector(),
            new FixedTimeProvider(FixedNow),
            NullLogger<BoardConnectionService>.Instance);

    private static SaveBoardConnectionRequest SaveRequest(
        string? personalAccessToken = "pat-0123456789",
        string projectName = "Delivery",
        BoardAssigneeFilter assigneeFilter = BoardAssigneeFilter.RosterMembersOnly,
        ContentSensitivityPolicy contentPolicy = ContentSensitivityPolicy.MetricsOnly,
        bool isEnabled = true) =>
        new(
            "https://dev.azure.com/contoso",
            projectName,
            TeamOrAreaPath: null,
            personalAccessToken,
            assigneeFilter,
            contentPolicy,
            isEnabled);

    private static ImportedWorkItem Imported(
        string externalId,
        string? assignedToDisplayName = null,
        string? iterationPath = null,
        WorkItemStatus status = WorkItemStatus.InProgress,
        string? boardState = "Active") =>
        new()
        {
            ExternalId = externalId,
            Key = $"AB#{externalId}",
            Title = $"AB#{externalId}",
            Status = status,
            BoardState = boardState,
            AssignedToDisplayName = assignedToDisplayName,
            IterationPath = iterationPath,
            CreatedAtUtc = FixedNow.AddDays(-7)
        };

    private async Task SeedRosterAsync(params TeamMember[] members)
    {
        await using var context = CreateContext();
        context.TeamMembers.AddRange(members);
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SaveConnection_KeepsTheStoredCredential_WhenNoNewTokenIsSupplied()
    {
        // Correcting a typo in the project name must not disconnect the board. Treating a null
        // token as "clear it" would do exactly that, and the damage would only surface at the
        // next sync, long after the edit that caused it.
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.SaveConnectionAsync(SaveRequest());
        var updated = await service.SaveConnectionAsync(SaveRequest(personalAccessToken: null, projectName: "Delivery Squad"));

        Assert.True(updated.HasStoredCredential);
        Assert.Equal("Delivery Squad", updated.ProjectName);

        await using var verification = CreateContext();
        var stored = await verification.BoardConnections.SingleAsync();
        Assert.Equal("enc:pat-0123456789", stored.EncryptedCredential);
    }

    [Fact]
    public async Task SaveConnection_ReplacesTheCredentialAndForgetsTheOldVerdict_WhenANewTokenArrives()
    {
        // The previous run's status described a different credential. Leaving it in place would
        // show a green tick against a token nobody has tested.
        await using var context = CreateContext();
        var service = CreateService(context);

        await service.SaveConnectionAsync(SaveRequest());

        await using (var seeding = CreateContext())
        {
            var connection = await seeding.BoardConnections.SingleAsync();
            connection.LastSyncStatus = "Connection verified";
            await seeding.SaveChangesAsync();
        }

        await using var replacing = CreateContext();
        var replaced = await CreateService(replacing).SaveConnectionAsync(SaveRequest(personalAccessToken: "pat-replacement"));

        Assert.Null(replaced.LastSyncStatus);

        await using var verification = CreateContext();
        Assert.Equal("enc:pat-replacement", (await verification.BoardConnections.SingleAsync()).EncryptedCredential);
    }

    [Fact]
    public async Task SaveConnection_ReturnsOnlyWhetherACredentialExists()
    {
        // A credential that can be read back can be exfiltrated through any read vulnerability,
        // so the DTO carries presence and nothing else - not the token, not a masked form of it.
        await using var context = CreateContext();

        var saved = await CreateService(context).SaveConnectionAsync(SaveRequest());

        Assert.True(saved.HasStoredCredential);

        var dtoProperties = saved.GetType().GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain(nameof(BoardConnection.EncryptedCredential), dtoProperties);
    }

    [Fact]
    public async Task SaveConnection_UpdatesTheOneRow_RatherThanAddingASecond()
    {
        // Two connection rows would mean two stored credentials, one of which nobody is
        // watching and no screen will ever show.
        await using (var first = CreateContext())
        {
            await CreateService(first).SaveConnectionAsync(SaveRequest());
        }

        await using (var second = CreateContext())
        {
            await CreateService(second).SaveConnectionAsync(SaveRequest(projectName: "Renamed"));
        }

        await using var verification = CreateContext();
        var connection = Assert.Single(await verification.BoardConnections.ToListAsync());
        Assert.Equal("Renamed", connection.ProjectName);
    }

    [Fact]
    public async Task SaveConnection_PrioritizesCredentialedConnectionAndRemovesStaleRows()
    {
        await using var seeding = CreateContext();
        seeding.BoardConnections.Add(new BoardConnection
        {
            Provider = WorkItemOrigin.FileImport,
            OrganizationUrl = string.Empty,
            ProjectName = string.Empty,
            EncryptedCredential = null,
            CreatedAtUtc = FixedNow.AddMinutes(-10),
            IsEnabled = true
        });
        await seeding.SaveChangesAsync();

        await using var context = CreateContext();
        var service = CreateService(context);
        var saved = await service.SaveConnectionAsync(SaveRequest(personalAccessToken: "pat-new-token", projectName: "Core"));

        Assert.True(saved.HasStoredCredential);

        var retrieved = await service.GetConnectionAsync();
        Assert.NotNull(retrieved);
        Assert.True(retrieved.HasStoredCredential);
        Assert.Equal("Core", retrieved.ProjectName);

        await using var verification = CreateContext();
        var allConnections = await verification.BoardConnections.ToListAsync();
        Assert.Single(allConnections);
        Assert.True(allConnections[0].HasStoredCredential);
    }

    [Fact]
    public async Task DeleteConnection_DestroysTheCredentialBeforeTheRowIsSoftDeleted()
    {
        // Soft delete leaves the row in the table. A disconnected board must not leave a usable
        // client credential sitting in a record that no longer appears anywhere in the UI.
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest());

        Assert.True(await service.DeleteConnectionAsync());

        await using var verification = CreateContext();
        var removed = await verification.BoardConnections.IgnoreQueryFilters().SingleAsync();

        Assert.True(removed.IsDeleted);
        Assert.False(removed.IsEnabled);
        Assert.Null(removed.EncryptedCredential);
    }

    [Fact]
    public async Task DeleteConnection_ReportsNothingToRemove_WhenNoneIsConfigured()
    {
        await using var context = CreateContext();

        Assert.False(await CreateService(context).DeleteConnectionAsync());
    }

    [Fact]
    public async Task Sync_ThrowsInvalidOperation_WhenNoBoardIsConnected()
    {
        // Translated to a 409 at the controller. The payload was fine; the squad simply has
        // nothing to sync, which is a state problem rather than a bad request.
        await using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(context).SyncAsync());
    }

    [Fact]
    public async Task Sync_ImportsNothingAndSaysWhy_WhenTheConnectionIsTurnedOff()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(isEnabled: false));

        var result = await service.SyncAsync();

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("turned off", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sync_ImportsNothingAndSaysWhy_WhenNoCredentialIsStored()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(personalAccessToken: null));

        var result = await service.SyncAsync();

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("access token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sync_SaysTheRosterHasNoEmailAddresses_RatherThanReportingACleanZero()
    {
        // The board query filters on identities, so an email-less roster returns nothing and
        // looks exactly like a board with no work on it. A squad would spend an afternoon
        // debugging the connection instead of filling in the roster.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = string.Empty });

        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(assigneeFilter: BoardAssigneeFilter.RosterMembersOnly));

        var result = await service.SyncAsync();

        Assert.Equal(0, result.ImportedCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("email address", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Sync_RecordsAGenericStatus_WhenTheBoardFails()
    {
        // The board's own message echoes back organisation names, project paths and identity
        // details, and the stored status is rendered in the UI. Writing the exception text
        // there would publish a client's internal naming to anyone who can see the screen.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        var syncClient = new FakeBoardSyncClient
        {
            FetchFailure = new HttpRequestException(
                "TF400813: The user 'svc-contoso@fabrikam.onmicrosoft.com' is not authorised to access https://dev.azure.com/contoso-internal.")
        };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());

        var result = await service.SyncAsync();

        Assert.Equal(0, result.ImportedCount);

        await using var verification = CreateContext();
        var status = (await verification.BoardConnections.SingleAsync()).LastSyncStatus;

        Assert.NotNull(status);
        Assert.DoesNotContain("TF400813", status, StringComparison.Ordinal);
        Assert.DoesNotContain("contoso-internal", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmicrosoft.com", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sync_UpsertsItemsAndSeparatesCreatedFromUpdated()
    {
        // The second run must refresh the same rows, not add a second copy of each. Matching on
        // the external id is what makes repeated syncs idempotent; matching on anything else
        // doubles the squad's apparent delivery on every run.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        var syncClient = new FakeBoardSyncClient { Items = [Imported("4182"), Imported("4183")] };

        await using (var first = CreateContext())
        {
            var service = CreateService(first, syncClient);
            await service.SaveConnectionAsync(SaveRequest());

            var initial = await service.SyncAsync();

            Assert.Equal(2, initial.ImportedCount);
            Assert.Equal(2, initial.CreatedCount);
        }

        syncClient.Items = [Imported("4182"), Imported("4183"), Imported("4184")];

        await using var second = CreateContext();
        var repeat = await CreateService(second, syncClient).SyncAsync();

        Assert.Equal(3, repeat.ImportedCount);
        Assert.Equal(1, repeat.CreatedCount);

        await using var verification = CreateContext();
        Assert.Equal(3, await verification.WorkItems.CountAsync());
    }

    [Fact]
    public async Task Sync_RecordsWhenItRanAndHowMuchItBrought()
    {
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        var syncClient = new FakeBoardSyncClient { Items = [Imported("4182"), Imported("4183")] };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());
        await service.SyncAsync();

        await using var verification = CreateContext();
        var connection = await verification.BoardConnections.SingleAsync();

        Assert.Equal(FixedNow, connection.LastSyncedAtUtc);
        Assert.Equal(2, connection.LastSyncItemCount);
        Assert.Contains("2", connection.LastSyncStatus!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Priya Raman")]
    [InlineData("priya.raman@contoso.com")]
    [InlineData("priya raman")]
    public async Task Sync_MatchesAnAssigneeByEveryNameABoardMightReport(string assignedToDisplayName)
    {
        // Boards show a display name, rosters hold a name and an email, and the two agree often
        // enough to be worth trying and differ often enough that one key is not sufficient. The
        // third case is the email local part with dots turned to spaces.
        var member = new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" };
        await SeedRosterAsync(member);

        var syncClient = new FakeBoardSyncClient
        {
            Items = [Imported("4182", assignedToDisplayName: assignedToDisplayName)]
        };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());

        var result = await service.SyncAsync();

        Assert.Empty(result.UnmatchedAssignees);

        await using var verification = CreateContext();
        Assert.Equal(member.Id, (await verification.WorkItems.SingleAsync()).AssigneeId);
    }

    [Fact]
    public async Task Sync_ImportsAnUnmatchedAssigneesItemUnattributedAndNamesThePerson()
    {
        // Guessing a member would put one person's work on another's record, which is far worse
        // than an obvious gap. Dropping the item would understate the squad's delivery. So the
        // item imports with no assignee and the name is reported so the roster can be fixed.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        var syncClient = new FakeBoardSyncClient
        {
            Items = [Imported("4182", assignedToDisplayName: "Client Developer")]
        };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());

        var result = await service.SyncAsync();

        Assert.Equal(1, result.ImportedCount);
        Assert.Contains("Client Developer", result.UnmatchedAssignees);

        await using var verification = CreateContext();
        Assert.Null((await verification.WorkItems.SingleAsync()).AssigneeId);
    }

    [Fact]
    public async Task Sync_MatchesAnIterationPathToASprintByItsLeaf()
    {
        // Iteration paths are backslash-separated and only the leaf is what teams name their
        // sprint. Comparing the whole path would leave every item outside its sprint and empty
        // every burndown.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        Guid sprintId;

        await using (var seeding = CreateContext())
        {
            var sprint = new Sprint
            {
                Name = "Sprint 12",
                StartDate = FixedNow.AddDays(-7),
                EndDate = FixedNow.AddDays(7)
            };

            seeding.Sprints.Add(sprint);
            await seeding.SaveChangesAsync();
            sprintId = sprint.Id;
        }

        var syncClient = new FakeBoardSyncClient
        {
            Items = [Imported("4182", iterationPath: @"Contoso\Sprint 12")]
        };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());
        await service.SyncAsync();

        await using var verification = CreateContext();
        Assert.Equal(sprintId, (await verification.WorkItems.SingleAsync()).SprintId);
    }

    [Fact]
    public async Task ImportFromFile_CreatesACredentiallessConnection_WhenNoneExists()
    {
        // A squad with no board access still needs somewhere for its state mapping and content
        // policy to live, so the first file import must not refuse until a board is configured.
        await using var context = CreateContext();

        var result = await CreateService(context).ImportFromFileAsync(
            "ID,Title,State\n4182,Totals,Active\n");

        Assert.Equal(1, result.ImportedCount);

        await using var verification = CreateContext();
        var connection = await verification.BoardConnections.SingleAsync();

        Assert.False(connection.HasStoredCredential);
        Assert.Equal(WorkItemOrigin.FileImport, connection.Provider);
    }

    [Fact]
    public async Task ImportFromFile_UnderRosterMembersOnly_DropsOutsidersKeepsUnassignedAndSaysHowMany()
    {
        // The live sync applies this filter in the board query, so the client's work never
        // travels. A file is exported by a person who may well have included the whole project,
        // so the rule has to be applied again on arrival - otherwise the easiest route is also
        // the one with the weakest guarantee. Unassigned items stay: they are usually the
        // squad's own unstarted backlog.
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(assigneeFilter: BoardAssigneeFilter.RosterMembersOnly));

        var result = await service.ImportFromFileAsync(
            """
            ID,Title,State,Assigned To
            4182,Ours,Active,Priya Raman <priya.raman@contoso.com>
            4183,Theirs,Active,Client Developer <dev@client.example>
            4184,Unclaimed,Active,
            """);

        Assert.Equal(2, result.ImportedCount);
        Assert.Contains(result.Warnings, warning => warning.Contains("outside this squad", StringComparison.OrdinalIgnoreCase));

        await using var verification = CreateContext();
        var externalIds = await verification.WorkItems.Select(item => item.ExternalId).ToListAsync();

        Assert.Contains("4182", externalIds);
        Assert.Contains("4184", externalIds);
        Assert.DoesNotContain("4183", externalIds);
    }

    [Fact]
    public async Task ImportFromFile_UpdatesExistingItems_WithoutDuplicating_WhenSameExternalIdImportedAgain()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(contentPolicy: ContentSensitivityPolicy.RedactedTitles));

        // First import
        var firstResult = await service.ImportFromFileAsync(
            """
            ID,Title,State,Story Points
            5001,Initial Feature,Active,3
            """);

        Assert.Equal(1, firstResult.ImportedCount);
        Assert.Equal(1, firstResult.CreatedCount);

        // Second import with updated title, state and an additional new item
        var secondResult = await service.ImportFromFileAsync(
            """
            ID,Title,State,Story Points
            5001,Updated Feature Name,Closed,5
            5002,Brand New Task,Active,2
            """);

        Assert.Equal(2, secondResult.ImportedCount);
        Assert.Equal(1, secondResult.CreatedCount); // Only 5002 is new; 5001 was updated

        await using var verification = CreateContext();
        var allItems = await verification.WorkItems.OrderBy(i => i.ExternalId).ToListAsync();
        Assert.Equal(2, allItems.Count);

        var updated = allItems.First(i => i.ExternalId == "5001");
        Assert.Equal("Updated Feature Name", updated.Title);
        Assert.Equal(WorkItemStatus.Done, updated.Status);
        Assert.Equal(5, updated.StoryPoints);
    }

    [Fact]
    public async Task ImportFromFile_ParsesVariedCsvColumns_StoryPointsAndIterationPath()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.SaveConnectionAsync(SaveRequest(contentPolicy: ContentSensitivityPolicy.RedactedTitles));

        var result = await service.ImportFromFileAsync(
            """
            ID,Work Item Type,Title,State,Story Points,Priority,Iteration Path,Tags
            6001,User Story,"Export Reports to PDF",Closed,"8",1,Contoso\Sprint 1,Reporting
            """);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.CreatedCount);

        await using var verification = CreateContext();
        var item = await verification.WorkItems.SingleAsync(i => i.ExternalId == "6001");
        Assert.Equal("Export Reports to PDF", item.Title);
        Assert.Equal(WorkItemStatus.Done, item.Status);
        Assert.Equal(8, item.StoryPoints);
    }

    [Fact]
    public async Task Sync_ReportsUnmappedStates_WhenItemsContainUnknownBoardColumns()
    {
        await SeedRosterAsync(new TeamMember { Name = "Priya Raman", Email = "priya.raman@contoso.com" });

        var syncClient = new FakeBoardSyncClient
        {
            Items = [Imported("7001", status: WorkItemStatus.Backlog, boardState: "In Security Review")]
        };

        await using var context = CreateContext();
        var service = CreateService(context, syncClient);
        await service.SaveConnectionAsync(SaveRequest());

        var result = await service.SyncAsync();

        Assert.Equal(1, result.ImportedCount);
        Assert.Contains("In Security Review", result.UnmappedStates);
    }

    /// <summary>Board client returning a scripted set of items, or a scripted failure.</summary>
    private sealed class FakeBoardSyncClient : IBoardSyncClient
    {
        public IReadOnlyList<ImportedWorkItem> Items { get; set; } = [];

        public Exception? FetchFailure { get; init; }

        public string? TestConnectionFailure { get; init; }

        public Task<string?> TestConnectionAsync(BoardConnection connection, CancellationToken ct = default) =>
            Task.FromResult(TestConnectionFailure);

        public Task<IReadOnlyList<ImportedWorkItem>> FetchWorkItemsAsync(
            BoardConnection connection,
            IReadOnlyCollection<string> rosterIdentities,
            DateTime? changedSince,
            CancellationToken ct = default) =>
            FetchFailure is null ? Task.FromResult(Items) : Task.FromException<IReadOnlyList<ImportedWorkItem>>(FetchFailure);

        public Task<IReadOnlyList<ImportedPullRequest>> FetchPullRequestsAsync(
            BoardConnection connection,
            IReadOnlyCollection<string> rosterIdentities,
            CancellationToken ct = default) =>
            FetchFailure is null ? Task.FromResult<IReadOnlyList<ImportedPullRequest>>([]) : Task.FromException<IReadOnlyList<ImportedPullRequest>>(FetchFailure);
    }


    /// <summary>
    /// Reversible stand-in for the real protector, so a test can assert which token was stored
    /// without depending on data protection keys.
    /// </summary>
    private sealed class PrefixCredentialProtector : ICredentialProtector
    {
        private const string Prefix = "enc:";

        public string Protect(string plainTextCredential) => Prefix + plainTextCredential;

        public string? Unprotect(string encryptedCredential) =>
            encryptedCredential.StartsWith(Prefix, StringComparison.Ordinal)
                ? encryptedCredential[Prefix.Length..]
                : null;
    }

    /// <summary>Clock pinned to a known instant, so a recorded sync time can be asserted exactly.</summary>
    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
