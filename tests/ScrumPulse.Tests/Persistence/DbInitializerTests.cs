namespace ScrumPulse.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class DbInitializerTests
{
    [Fact]
    public async Task SeedAsync_DefaultPublicMode_LeavesRosterCleanForScrumMaster()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_CleanDb_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        // Default Seed (Public Website mode)
        await DbInitializer.SeedAsync(db);

        // Team members are NOT seeded by default - squad starts clean for the Scrum Master
        Assert.False(await db.TeamMembers.AnyAsync());
        Assert.False(await db.WorkItems.AnyAsync());
        Assert.False(await db.Blockers.AnyAsync());
    }

    [Fact]
    public async Task SeedAsync_InDefaultPublicMode_CleansUpLegacyDemoMembers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_LegacyDb_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        // Pre-populate with legacy demo members
        db.TeamMembers.AddRange(
            new ScrumPulse.Domain.Entities.TeamMember { Name = "Kaushik", Email = "kaushik.dev@scrumpulse.io", Role = ScrumPulse.Domain.Enums.RoleType.Developer },
            new ScrumPulse.Domain.Entities.TeamMember { Name = "Angan", Email = "angan.qa@scrumpulse.io", Role = ScrumPulse.Domain.Enums.RoleType.QaEngineer },
            new ScrumPulse.Domain.Entities.TeamMember { Name = "Alice (Real User)", Email = "alice@company.com", Role = ScrumPulse.Domain.Enums.RoleType.Developer }
        );
        await db.SaveChangesAsync();

        // Run default public mode seeding
        await DbInitializer.SeedAsync(db, seedDemoData: false);

        // Legacy demo members should be soft-deleted and filtered out, while real team members remain
        var remainingMembers = await db.TeamMembers.ToListAsync();
        Assert.Single(remainingMembers);
        Assert.Equal("alice@company.com", remainingMembers[0].Email);
    }

    [Fact]
    public async Task SeedAsync_WithDemoDataFlag_PopulatesDemoSquadIdempotently()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_DemoDb_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        // Seed with demo flag
        await DbInitializer.SeedAsync(db, seedDemoData: true);

        Assert.True(await db.TeamMembers.AnyAsync());
        var memberCount = await db.TeamMembers.CountAsync();
        Assert.Equal(4, memberCount);

        // Second Seed (Idempotency check)
        await DbInitializer.SeedAsync(db, seedDemoData: true);
        var memberCountAfterSecondSeed = await db.TeamMembers.CountAsync();

        Assert.Equal(memberCount, memberCountAfterSecondSeed);
    }

    [Fact]
    public async Task EnsureSchemaUpToDateAsync_MigratesMissingColumnsInSqlite()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        try
        {
            // Create a minimal WorkItems table without EstimatedHours and audit columns
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE ""WorkItems"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""Key"" TEXT NOT NULL,
                        ""Title"" TEXT NOT NULL,
                        ""Description"" TEXT NOT NULL,
                        ""Type"" INTEGER NOT NULL,
                        ""Status"" INTEGER NOT NULL,
                        ""Priority"" INTEGER NOT NULL,
                        ""StoryPoints"" INTEGER NOT NULL,
                        ""DorAcceptanceCriteriaDefined"" INTEGER NOT NULL DEFAULT 1,
                        ""DorDependenciesIdentified"" INTEGER NOT NULL DEFAULT 1,
                        ""DorWireframeAvailable"" INTEGER NOT NULL DEFAULT 1,
                        ""DodUnitTestsPassed"" INTEGER NOT NULL DEFAULT 0,
                        ""DodPeerReviewCompleted"" INTEGER NOT NULL DEFAULT 0,
                        ""DodMergedToMaster"" INTEGER NOT NULL DEFAULT 0,
                        ""DodStagingVerified"" INTEGER NOT NULL DEFAULT 0,
                        ""IsEscapedDefect"" INTEGER NOT NULL DEFAULT 0,
                        ""CreatedAtUtc"" TEXT NOT NULL,
                        ""IsDeleted"" INTEGER NOT NULL DEFAULT 0
                    );
                ";
                await cmd.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using var db = new AppDbContext(options);

            // Execute schema migration
            await DbInitializer.EnsureSchemaUpToDateAsync(db);

            // Verify EstimatedHours column was added by inserting and querying a WorkItem
            var workItem = new ScrumPulse.Domain.Entities.WorkItem
            {
                Key = "SP-101",
                Title = "Test Migration WorkItem",
                Description = "Testing dynamic column addition",
                EstimatedHours = 4.5
            };
            db.WorkItems.Add(workItem);
            await db.SaveChangesAsync();

            var loaded = await db.WorkItems.FirstAsync(w => w.Key == "SP-101");
            Assert.NotNull(loaded);
            Assert.Equal(4.5, loaded.EstimatedHours);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task EnsureSchemaUpToDateAsync_CreatesPullRequestReviewLogsTableAndAllowsQuery()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            using var db = new AppDbContext(options);

            // Execute schema migration
            await DbInitializer.EnsureSchemaUpToDateAsync(db);

            // Add a PullRequestReviewLog
            var pr = new ScrumPulse.Domain.Entities.PullRequestReviewLog
            {
                PrNumber = "42",
                PrTitle = "Fix bug",
                PrUrl = "https://github.com/org/repo/pull/42",
                AuthorId = Guid.NewGuid(),
                TotalCommentsCount = 5,
                ActionableCommentsCount = 2,
                ReviewSummary = "LGTM",
                ReviewStatus = ScrumPulse.Domain.Enums.ReviewStatusType.Approved
            };
            db.PullRequestReviewLogs.Add(pr);
            await db.SaveChangesAsync();

            var loaded = await db.PullRequestReviewLogs.FirstOrDefaultAsync();
            Assert.NotNull(loaded);
            Assert.Equal("42", loaded.PrNumber);
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    [Fact]
    public void SchemaDialectFactory_ResolvesSqliteDialect()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var db = new AppDbContext(options);
        var dialect = ScrumPulse.Infrastructure.Persistence.Dialects.SchemaDialectFactory.GetDialect(db.Database);

        Assert.NotNull(dialect);
        Assert.IsType<ScrumPulse.Infrastructure.Persistence.Dialects.SqliteSchemaDialect>(dialect);
    }

    [Fact]
    public void SqliteSchemaDialect_MapsTypesCorrectly()
    {
        var dialect = new ScrumPulse.Infrastructure.Persistence.Dialects.SqliteSchemaDialect();

        Assert.Equal("INTEGER NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(bool), false));
        Assert.Equal("INTEGER NULL", dialect.MapToSqlType(typeof(bool), true));
        Assert.Equal("INTEGER NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(int), false));
        Assert.Equal("REAL NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(double), false));
        Assert.Equal("BLOB NULL", dialect.MapToSqlType(typeof(byte[]), true));
        Assert.Equal("TEXT NOT NULL DEFAULT ''", dialect.MapToSqlType(typeof(string), false));
        Assert.Equal("TEXT NULL", dialect.MapToSqlType(typeof(string), true));
        Assert.Equal("ALTER TABLE \"WorkItems\" ADD COLUMN \"Points\" INTEGER NOT NULL DEFAULT 0;",
            dialect.BuildAddColumnSql("WorkItems", "Points", "INTEGER NOT NULL DEFAULT 0"));
    }

    [Fact]
    public void PostgresSchemaDialect_MapsTypesCorrectly()
    {
        var dialect = new ScrumPulse.Infrastructure.Persistence.Dialects.PostgresSchemaDialect();

        Assert.Equal("boolean NOT NULL DEFAULT false", dialect.MapToSqlType(typeof(bool), false));
        Assert.Equal("boolean NULL", dialect.MapToSqlType(typeof(bool), true));
        Assert.Equal("integer NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(int), false));
        Assert.Equal("bigint NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(long), false));
        Assert.Equal("double precision NOT NULL DEFAULT 0", dialect.MapToSqlType(typeof(double), false));
        Assert.Equal("bytea NULL", dialect.MapToSqlType(typeof(byte[]), true));
        Assert.Equal("uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'", dialect.MapToSqlType(typeof(Guid), false));
        Assert.Equal("timestamp with time zone NOT NULL DEFAULT NOW()", dialect.MapToSqlType(typeof(DateTime), false));
        Assert.Equal("text NOT NULL DEFAULT ''", dialect.MapToSqlType(typeof(string), false));
        Assert.Equal("text NULL", dialect.MapToSqlType(typeof(string), true));
        Assert.Equal("ALTER TABLE \"WorkItems\" ADD COLUMN IF NOT EXISTS \"Points\" integer NOT NULL DEFAULT 0;",
            dialect.BuildAddColumnSql("WorkItems", "Points", "integer NOT NULL DEFAULT 0"));
    }
}
