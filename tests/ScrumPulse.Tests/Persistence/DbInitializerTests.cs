namespace ScrumPulse.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Infrastructure.Persistence;
using Xunit;

public class DbInitializerTests
{
    [Fact]
    public async Task SeedAsync_PopulatesInitialEnterpriseDataIdempotently()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_SeedDb_{Guid.NewGuid()}")
            .Options;

        using var db = new AppDbContext(options);

        // First Seed
        await DbInitializer.SeedAsync(db);

        // Team members are seeded
        Assert.True(await db.TeamMembers.AnyAsync());
        Assert.Equal(7, await db.TeamMembers.CountAsync());

        // Test/mock items are cleaned to ensure clean user workspace
        Assert.False(await db.WorkItems.AnyAsync());
        Assert.False(await db.Blockers.AnyAsync());

        var memberCount = await db.TeamMembers.CountAsync();

        // Second Seed (Idempotency check)
        await DbInitializer.SeedAsync(db);
        var memberCountAfterSecondSeed = await db.TeamMembers.CountAsync();

        Assert.Equal(memberCount, memberCountAfterSecondSeed);
    }
}
