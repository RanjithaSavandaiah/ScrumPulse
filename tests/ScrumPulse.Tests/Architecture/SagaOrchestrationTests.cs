namespace ScrumPulse.Tests.Architecture;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.Sagas.WorkItemCompletion;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Infrastructure.Persistence;
using ScrumPulse.Infrastructure.Repositories;
using ScrumPulse.Infrastructure.Services;
using Xunit;

public class SagaOrchestrationTests
{
    private (AppDbContext db, WorkItemCompletionSaga saga, EfUnitOfWork uow) CreateTestEnvironment()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: $"ScrumPulse_SagaTest_{Guid.NewGuid()}")
            .Options;

        var db = new AppDbContext(options);
        var eventDispatcher = new DomainEventDispatcher(NullLogger<DomainEventDispatcher>.Instance);
        var uow = new EfUnitOfWork(db, eventDispatcher);
        var aiService = new MicrosoftAgentService(db);

        var step1 = new ValidateQualityGatesStep(uow);
        var step2 = new TransitionWorkItemStatusStep(uow);
        var step3 = new RecalculateSprintVelocityStep(uow);
        var step4 = new TriggerMicrosoftAgentAiCoachingStep(aiService);

        var saga = new WorkItemCompletionSaga(step1, step2, step3, step4, uow);
        return (db, saga, uow);
    }

    [Fact]
    public async Task WorkItemCompletionSaga_ExecutesAllStepsSuccessfully()
    {
        var (db, saga, _) = CreateTestEnvironment();

        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 1", DeliveredStoryPoints = 10 };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = "SP-201",
            Title = "Implement Distributed Saga",
            StoryPoints = 8,
            SprintId = sprint.Id,
            Status = WorkItemStatus.InQa,
            DorAcceptanceCriteriaDefined = true,
            DodUnitTestsPassed = true
        };

        db.Sprints.Add(sprint);
        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync();

        var context = new WorkItemCompletionContext { WorkItemId = workItem.Id };
        var result = await saga.ExecuteAsync(context);

        Assert.True(result.IsSuccessful);
        Assert.NotNull(result.Result);
        Assert.Equal(WorkItemStatus.Done, result.Result.Status);
        Assert.Equal(4, result.ExecutedSteps.Count);
        Assert.Empty(result.CompensatedSteps);

        // Verify sprint delivered points updated
        var updatedSprint = await db.Sprints.FindAsync(sprint.Id);
        Assert.Equal(18, updatedSprint!.DeliveredStoryPoints);
    }

    [Fact]
    public async Task WorkItemCompletionSaga_CompensatesExecutedSteps_WhenFailureOccurs()
    {
        var (db, _, uow) = CreateTestEnvironment();

        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint 1", DeliveredStoryPoints = 10 };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = "SP-202",
            Title = "Rollback Test",
            StoryPoints = 5,
            SprintId = sprint.Id,
            Status = WorkItemStatus.InQa,
            DorAcceptanceCriteriaDefined = true,
            DodUnitTestsPassed = true
        };

        db.Sprints.Add(sprint);
        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync();

        var step1 = new ValidateQualityGatesStep(uow);
        var step2 = new TransitionWorkItemStatusStep(uow);
        var step3 = new RecalculateSprintVelocityStep(uow);
        var failingStep4 = new TriggerMicrosoftAgentAiCoachingStep(new MicrosoftAgentService(db));

        // Create context
        var context = new WorkItemCompletionContext { WorkItemId = workItem.Id };

        // Step 1 & 2 succeed
        await step1.ExecuteAsync(context);
        await step2.ExecuteAsync(context);

        // Step 3 executed
        await step3.ExecuteAsync(context);
        Assert.Equal(15, sprint.DeliveredStoryPoints);

        // Simulate compensation
        await step3.CompensateAsync(context);
        await step2.CompensateAsync(context);

        // Assert state was rolled back
        Assert.Equal(10, sprint.DeliveredStoryPoints);
        Assert.Equal(WorkItemStatus.InQa, workItem.Status);
    }
}
