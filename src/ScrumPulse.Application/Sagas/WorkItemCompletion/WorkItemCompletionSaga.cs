namespace ScrumPulse.Application.Sagas.WorkItemCompletion;

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
    IUnitOfWork unitOfWork
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
                    await CompensateExecutedStepsAsync(steps, executedSteps, compensatedSteps, context, ct);
                    return new SagaExecutionResult<WorkItemDto>(false, null, $"Saga failed at step: {step.StepName}", executedSteps, compensatedSteps);
                }
                executedSteps.Add(step.StepName);
            }
            catch (Exception ex)
            {
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

    private static async Task CompensateExecutedStepsAsync(
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
                catch
                {
                    // Compensation resilience — log but don't throw
                }
            }
        }
    }
}
