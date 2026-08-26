namespace ScrumPulse.Application.Sagas.WorkItemCompletion;

using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;

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

        // Fetch refreshed work item with relations
        var workItemRepo = unitOfWork.Repository<WorkItem>();
        var refreshedItem = await workItemRepo.FirstOrDefaultAsync(new WorkItemWithRelationsByIdSpecification(context.WorkItemId), ct);

        var item = refreshedItem ?? context.WorkItem!;
        var dto = new WorkItemDto(
            item.Id, item.Key, item.Title, item.Description,
            item.Type, item.Status, item.Priority, item.StoryPoints,
            item.AssigneeId, item.Assignee?.Name, item.SprintId,
            item.PrNumber, item.PrUrl, item.PrBranch, item.TargetBranch,
            item.PrReviewerId, item.PrReviewer?.Name,
            item.CreatedAtUtc, item.PickedUpAtUtc, item.PrCreatedAtUtc,
            item.PrApprovedAtUtc, item.PrMergedAtUtc, item.QaStartedAtUtc,
            item.CompletedAtUtc,
            item.DorAcceptanceCriteriaDefined, item.DorDependenciesIdentified,
            item.DorWireframeAvailable, item.DodUnitTestsPassed,
            item.DodPeerReviewCompleted, item.DodMergedToMaster,
            item.DodStagingVerified, item.IsEscapedDefect, item.DefectRootCause,
            item.PickupLatencyHours, item.DevCycleTimeHours,
            item.PrReviewLatencyHours, item.PrMergeLatencyHours,
            item.QaTestingLatencyHours, item.TotalCycleTimeHours,
            item.EstimatedHours
        );

        return new SagaExecutionResult<WorkItemDto>(true, dto, null, executedSteps, compensatedSteps);
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
                catch
                {
                    // Compensation resilience
                }
            }
        }
    }
}
