namespace ScrumPulse.Tests.Architecture;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ScrumPulse.AI.Configuration;
using ScrumPulse.AI.Services;
using ScrumPulse.Application.Sagas;
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
        var eventDispatcher = new DomainEventDispatcher(
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<DomainEventDispatcher>.Instance);
        var uow = new EfUnitOfWork(db, eventDispatcher, NullLogger<EfUnitOfWork>.Instance);
        var store = new MemoryIdempotencyStore();
        var agentConfig = new AgentConfiguration();
        var aiService = new MicrosoftAgentService(db, store, agentConfig, NullLogger<MicrosoftAgentService>.Instance);

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
        var store = new MemoryIdempotencyStore();
        var agentConfig = new AgentConfiguration();

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

    [Fact]
    public async Task WorkItemCompletionSaga_WhenWorkItemNotFound_FailsAtStep1WithoutCompensation()
    {
        var (_, saga, _) = CreateTestEnvironment();
        var context = new WorkItemCompletionContext { WorkItemId = Guid.NewGuid() };

        var result = await saga.ExecuteAsync(context);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Result);
        Assert.Contains("ValidateQualityGates", result.ErrorMessage);
        Assert.Empty(result.ExecutedSteps);
        Assert.Empty(result.CompensatedSteps);
    }

    [Fact]
    public async Task WorkItemCompletionSaga_WhenStepThrows_CompensatesPreviousStepsAndReturnsFailure()
    {
        var (db, _, uow) = CreateTestEnvironment();

        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint Error Test", DeliveredStoryPoints = 5 };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = "SP-301",
            Title = "Error Recovery Test",
            StoryPoints = 3,
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
        var faultyStep = new FaultyStep("FaultyExternalService");

        var customSaga = new WorkItemCompletionSaga([step1, step2, faultyStep], uow);
        var context = new WorkItemCompletionContext { WorkItemId = workItem.Id };

        var result = await customSaga.ExecuteAsync(context);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.Result);
        Assert.Contains("Exception at step FaultyExternalService", result.ErrorMessage);
        Assert.Contains("TransitionWorkItemStatus", result.CompensatedSteps);

        // Verify status rolled back to InQa
        var updatedItem = await db.WorkItems.FindAsync(workItem.Id);
        Assert.Equal(WorkItemStatus.InQa, updatedItem!.Status);
    }

    [Fact]
    public async Task WorkItemCompletionSaga_WhenStepReturnsFalse_TriggersCompensationInReverseOrder()
    {
        var (db, _, uow) = CreateTestEnvironment();

        var sprint = new Sprint { Id = Guid.NewGuid(), Name = "Sprint Order Test", DeliveredStoryPoints = 20 };
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            Key = "SP-302",
            Title = "Reverse Order Test",
            StoryPoints = 4,
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
        var failingStep = new FailingStep("FailingFinalStep");

        var customSaga = new WorkItemCompletionSaga([step1, step2, step3, failingStep], uow);
        var context = new WorkItemCompletionContext { WorkItemId = workItem.Id };

        var result = await customSaga.ExecuteAsync(context);

        Assert.False(result.IsSuccessful);
        Assert.Equal(3, result.ExecutedSteps.Count);
        // Compensated steps in reverse order: step 3 first, then step 2
        Assert.Equal("RecalculateSprintVelocity", result.CompensatedSteps[0]);
        Assert.Equal("TransitionWorkItemStatus", result.CompensatedSteps[1]);

        // Verify sprint delivered points rolled back to 20
        var updatedSprint = await db.Sprints.FindAsync(sprint.Id);
        Assert.Equal(20, updatedSprint!.DeliveredStoryPoints);
    }

    private sealed class FaultyStep(string name) : ISagaStep<WorkItemCompletionContext>
    {
        public string StepName => name;
        public Task<bool> ExecuteAsync(WorkItemCompletionContext context, CancellationToken ct = default)
            => throw new InvalidOperationException($"Step {name} failed with simulated exception");
        public Task CompensateAsync(WorkItemCompletionContext context, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FailingStep(string name) : ISagaStep<WorkItemCompletionContext>
    {
        public string StepName => name;
        public Task<bool> ExecuteAsync(WorkItemCompletionContext context, CancellationToken ct = default)
            => Task.FromResult(false);
        public Task CompensateAsync(WorkItemCompletionContext context, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
