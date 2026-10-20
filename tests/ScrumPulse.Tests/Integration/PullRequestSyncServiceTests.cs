namespace ScrumPulse.Tests.Integration;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Integration;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class PullRequestSyncServiceTests
{
    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_PrSync_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private sealed class FakeBoardSyncClient : IBoardSyncClient
    {
        public IReadOnlyList<ImportedPullRequest> PullRequests { get; set; } = [];
        public Task<string?> TestConnectionAsync(BoardConnection connection, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<ImportedWorkItem>> FetchWorkItemsAsync(BoardConnection connection, IReadOnlyCollection<string> rosterIdentities, DateTime? changedSince, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ImportedWorkItem>>([]);
        public Task<IReadOnlyList<ImportedPullRequest>> FetchPullRequestsAsync(BoardConnection connection, IReadOnlyCollection<string> rosterIdentities, CancellationToken ct = default) => Task.FromResult(PullRequests);
    }

    [Fact]
    public void Parser_ParsesValidCsvWithQuotesAndCommentMetrics()
    {
        var csv = """
            "PR Number","Title","Author","Reviewer","Total Comments","Actionable Comments","Status","URL","Review Summary"
            "#101","Add distributed caching, with retry policy","r.savandaiah@devon.nl","john@devon.nl","12","5","Merged","https://dev.azure.com/org/p/_git/r/pullrequest/101","Refactored memory cache"
            "102","Fix null reference in payment gateway","jane@devon.nl","","8","2","Approved","https://dev.azure.com/org/p/_git/r/pullrequest/102",""
            """;

        var result = PullRequestExportParser.Parse(csv);

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Items.Count);

        var item1 = result.Items[0];
        Assert.Equal("#101", item1.PrNumber);
        Assert.Equal("Add distributed caching, with retry policy", item1.Title);
        Assert.Equal("r.savandaiah@devon.nl", item1.Author);
        Assert.Equal("john@devon.nl", item1.Reviewer);
        Assert.Equal(12, item1.TotalCommentsCount);
        Assert.Equal(5, item1.ActionableCommentsCount);
        Assert.Equal("Merged", item1.ReviewStatus);
        Assert.Equal("https://dev.azure.com/org/p/_git/r/pullrequest/101", item1.PrUrl);

        var item2 = result.Items[1];
        Assert.Equal("#102", item2.PrNumber);
        Assert.Equal("Fix null reference in payment gateway", item2.Title);
        Assert.Equal("jane@devon.nl", item2.Author);
        Assert.Null(item2.Reviewer);
        Assert.Equal(8, item2.TotalCommentsCount);
        Assert.Equal(2, item2.ActionableCommentsCount);
        Assert.Equal("Approved", item2.ReviewStatus);
    }

    [Fact]
    public void Parser_ReturnsWarnings_WhenFileEmptyOrMissingRequiredHeaders()
    {
        var emptyResult = PullRequestExportParser.Parse("");
        Assert.NotEmpty(emptyResult.Warnings);

        var noHeaderResult = PullRequestExportParser.Parse("col1,col2,col3\n1,2,3");
        Assert.NotEmpty(noHeaderResult.Warnings);
    }

    [Fact]
    public async Task SyncFromAzureDevOps_ThrowsInvalidOp_WhenNoBoardConnectionConfigured()
    {
        using var db = CreateInMemoryDb();
        var client = new FakeBoardSyncClient();
        var service = new PullRequestSyncService(db, client, TimeProvider.System, NullLogger<PullRequestSyncService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncFromAzureDevOpsAsync());
    }

    [Fact]
    public async Task SyncFromAzureDevOps_ImportsAndAttributesToRosterMembers()
    {
        using var db = CreateInMemoryDb();

        var author = new TeamMember { Name = "Ranjitha", Email = "r.savandaiah@devon.nl", Role = RoleType.Developer };
        var reviewer = new TeamMember { Name = "Bob", Email = "bob@devon.nl", Role = RoleType.Developer };
        var sprint = new Sprint { Name = "Sprint 1", StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(9), IsActive = true };

        var connection = new BoardConnection
        {
            OrganizationUrl = "https://dev.azure.com/contoso",
            ProjectName = "Delivery Platform",
            EncryptedCredential = "dummy-token",
            IsEnabled = true
        };

        db.TeamMembers.AddRange(author, reviewer);
        db.Sprints.Add(sprint);
        db.BoardConnections.Add(connection);
        await db.SaveChangesAsync();

        var client = new FakeBoardSyncClient
        {
            PullRequests =
            [
                new ImportedPullRequest(
                    ExternalPrId: "501",
                    PrNumber: "#501",
                    Title: "Implement resilient message handler",
                    Url: "https://dev.azure.com/contoso/Delivery%20Platform/_git/Repo/pullrequest/501",
                    AuthorIdentity: "r.savandaiah@devon.nl",
                    AuthorDisplayName: "Ranjitha",
                    ReviewerIdentity: "bob@devon.nl",
                    ReviewerDisplayName: "Bob",
                    CreatedAtUtc: DateTime.UtcNow.AddDays(-2),
                    ClosedAtUtc: DateTime.UtcNow.AddDays(-1),
                    Status: "completed",
                    TotalCommentsCount: 6,
                    ActionableCommentsCount: 3,
                    ReviewSummary: "Themes: TestCoverage, ErrorHandling",
                    Comments: []
                )
            ]
        };

        var service = new PullRequestSyncService(db, client, TimeProvider.System, NullLogger<PullRequestSyncService>.Instance);
        var result = await service.SyncFromAzureDevOpsAsync();

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);

        var saved = await db.PullRequestReviewLogs.SingleAsync();
        Assert.Equal("#501", saved.PrNumber);
        Assert.Equal("Implement resilient message handler", saved.PrTitle);
        Assert.Equal(author.Id, saved.AuthorId);
        Assert.Equal(reviewer.Id, saved.ReviewerId);
        Assert.Equal(sprint.Id, saved.SprintId);
        Assert.Equal(ReviewStatusType.Merged, saved.ReviewStatus);
        Assert.Equal(6, saved.TotalCommentsCount);
        Assert.Equal(3, saved.ActionableCommentsCount);

        // Re-sync idempotency test
        client.PullRequests =
        [
            new ImportedPullRequest(
                ExternalPrId: "501",
                PrNumber: "#501",
                Title: "Implement resilient message handler (updated)",
                Url: "https://dev.azure.com/contoso/Delivery%20Platform/_git/Repo/pullrequest/501",
                AuthorIdentity: "r.savandaiah@devon.nl",
                AuthorDisplayName: "Ranjitha",
                ReviewerIdentity: "bob@devon.nl",
                ReviewerDisplayName: "Bob",
                CreatedAtUtc: DateTime.UtcNow.AddDays(-2),
                ClosedAtUtc: DateTime.UtcNow.AddDays(-1),
                Status: "completed",
                TotalCommentsCount: 10,
                ActionableCommentsCount: 5,
                ReviewSummary: "Themes: TestCoverage, ErrorHandling",
                Comments: []
            )
        ];

        var resyncResult = await service.SyncFromAzureDevOpsAsync();
        Assert.Equal(1, resyncResult.ImportedCount);
        Assert.Equal(0, resyncResult.CreatedCount);
        Assert.Equal(1, resyncResult.UpdatedCount);

        var updated = await db.PullRequestReviewLogs.SingleAsync();
        Assert.Equal("Implement resilient message handler (updated)", updated.PrTitle);
        Assert.Equal(10, updated.TotalCommentsCount);
        Assert.Equal(5, updated.ActionableCommentsCount);
    }

    [Fact]
    public async Task ImportFromCsv_ImportsAndAttributesPullRequestsIdempotently()
    {
        using var db = CreateInMemoryDb();

        var author = new TeamMember { Name = "Alice Dev", Email = "alice@example.com", Role = RoleType.Developer };
        var sprint = new Sprint { Name = "Sprint 42", StartDate = DateTime.UtcNow.AddDays(-7), EndDate = DateTime.UtcNow.AddDays(7), IsActive = true };

        db.TeamMembers.Add(author);
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync();

        var csv = """
            "PR Number","Title","Author","Sprint","Total Comments","Actionable Comments","Status","URL"
            "#888","Add telemetry middleware","alice@example.com","Sprint 42","7","4","Approved","https://github.com/org/repo/pull/888"
            """;

        var client = new FakeBoardSyncClient();
        var service = new PullRequestSyncService(db, client, TimeProvider.System, NullLogger<PullRequestSyncService>.Instance);

        var result = await service.ImportFromCsvAsync(csv);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.CreatedCount);

        var log = await db.PullRequestReviewLogs.SingleAsync();
        Assert.Equal("#888", log.PrNumber);
        Assert.Equal("Add telemetry middleware", log.PrTitle);
        Assert.Equal(author.Id, log.AuthorId);
        Assert.Equal(sprint.Id, log.SprintId);
        Assert.Equal(7, log.TotalCommentsCount);
        Assert.Equal(4, log.ActionableCommentsCount);
        Assert.Equal(ReviewStatusType.Approved, log.ReviewStatus);
    }
}
