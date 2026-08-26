namespace ScrumPulse.Tests.Architecture;

using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Repositories;
using Xunit;

public class SpecificationPatternTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_SpecTest_{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task WorkItemsFilterSpecification_FiltersAndIncludesAccurately()
    {
        using var db = CreateDbContext();
        var repo = new EfRepository<WorkItem>(db);
        var sprintId = Guid.NewGuid();

        var item1 = new WorkItem { Id = Guid.NewGuid(), Title = "Item 1", SprintId = sprintId, Status = WorkItemStatus.InProgress };
        var item2 = new WorkItem { Id = Guid.NewGuid(), Title = "Item 2", SprintId = sprintId, Status = WorkItemStatus.Done };
        var item3 = new WorkItem { Id = Guid.NewGuid(), Title = "Item 3", SprintId = Guid.NewGuid(), Status = WorkItemStatus.InProgress };

        db.WorkItems.AddRange(item1, item2, item3);
        await db.SaveChangesAsync();

        var spec = new WorkItemsFilterSpecification(sprintId, WorkItemStatus.InProgress);
        var results = await repo.ListAsync(spec);

        Assert.Single(results);
        Assert.Equal("Item 1", results[0].Title);
    }
}
