namespace ScrumPulse.Application.Sagas.WorkItemCompletion;

using Microsoft.Extensions.Logging;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

/// <summary>
/// Saga orchestrator for the WorkItem completion lifecycle.
/// Executes quality gates, status transition, velocity recalculation,
/// and AI coaching in sequence with compensation on failure.
/// </summary>
public class WorkItemCompletionSaga(
    ValidateQualityGatesStep step1,
    TransitionWorkItemStatusStep step2,
    RecalculateSprintVelocityStep step3,
    TriggerMicrosoftAgentAiCoachingStep step4,
    IUnitOfWork unitOfWork,
    ILogger<WorkItemCompletionSaga>? logger = null
) : ISagaOrchestrator<WorkItemCompletionContext, WorkItemDto>
{
    public async Task<SagaExecutionResult<WorkItemDto>> ExecuteAsync(WorkItemCompletionContext context, CancellationToken ct = default)
    {
        var steps = new List<ISagaStep<WorkItemCompletionContext>> { step1, step2, step3, step4 };
        var executedSteps = new List<string>();
        var compensatedSteps = new List<string>();

        foreach (var step in steps)
        {
            try
            {
                var success = await step.ExecuteAsync(context, ct);
                if (!success)
                {
                    logger?.LogWarning("Saga failed at step {StepName} for work item {WorkItemId}", step.StepName, context.WorkItemId);
                    await CompensateExecutedStepsAsync(steps, executedSteps, compensatedSteps, context, ct);
                    return new SagaExecutionResult<WorkItemDto>(false, null, $"Saga failed at step: {step.StepName}", executedSteps, compensatedSteps);
                }
                executedSteps.Add(step.StepName);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Unhandled exception at saga step {StepName} for work item {WorkItemId}", step.StepName, context.WorkItemId);
                await CompensateExecutedStepsAsync(steps, executedSteps, compensatedSteps, context, ct);
                return new SagaExecutionResult<WorkItemDto>(false, null, $"Exception at step {step.StepName}: {ex.Message}", executedSteps, compensatedSteps);
            }
        }

        // Fetch refreshed work item with relations for accurate DTO mapping
        var workItemRepo = unitOfWork.Repository<WorkItem>();
        var refreshedItem = await workItemRepo.FirstOrDefaultAsync(new WorkItemWithRelationsByIdSpecification(context.WorkItemId), ct);
        var item = refreshedItem ?? context.WorkItem!;

        return new SagaExecutionResult<WorkItemDto>(true, item.ToDto(), null, executedSteps, compensatedSteps);
    }

    private async Task CompensateExecutedStepsAsync(
        List<ISagaStep<WorkItemCompletionContext>> steps,
        List<string> executedSteps,
        List<string> compensatedSteps,
        WorkItemCompletionContext context,
        CancellationToken ct)
    {
        for (int i = steps.Count - 1; i >= 0; i--)
        {
            var step = steps[i];
            if (executedSteps.Contains(step.StepName))
            {
                try
                {
                    await step.CompensateAsync(context, ct);
                    compensatedSteps.Add(step.StepName);
                }
                catch (Exception ex)
                {
                    // Compensation resilience — log explicitly so failures are visible in telemetry
                    logger?.LogError(ex, "Failed to compensate saga step {StepName} for work item {WorkItemId}", step.StepName, context.WorkItemId);
                }
            }
        }
    }
}
