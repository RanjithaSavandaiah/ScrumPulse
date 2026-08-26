namespace ScrumPulse.Application.CQRS.WorkItems;

using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Sagas.WorkItemCompletion;
using ScrumPulse.Application.Specifications;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Domain.Entities;
using ScrumPulse.Domain.Enums;
using ScrumPulse.Domain.Events;

public record GetWorkItemsQuery(Guid? SprintId, WorkItemStatus? Status) : IQuery<IEnumerable<WorkItemDto>>;

public class GetWorkItemsQueryHandler(IUnitOfWork unitOfWork) : IQueryHandler<GetWorkItemsQuery, IEnumerable<WorkItemDto>>
{
    public async Task<IEnumerable<WorkItemDto>> HandleAsync(GetWorkItemsQuery query, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<WorkItem>();
        var items = await repo.ListAsync(new WorkItemsFilterSpecification(query.SprintId, query.Status), ct);

        return items.Select(item => new WorkItemDto(
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
        ));
    }
}

public record CreateWorkItemCommand(CreateWorkItemRequest Request) : ICommand<WorkItemDto>;

public class CreateWorkItemCommandHandler(IUnitOfWork unitOfWork) : ICommandHandler<CreateWorkItemCommand, WorkItemDto>
{
    public async Task<WorkItemDto> HandleAsync(CreateWorkItemCommand command, CancellationToken ct = default)
    {
        var repo = unitOfWork.Repository<WorkItem>();
        var totalCount = await repo.CountAsync(null, ct) + 101;

        var workItem = new WorkItem
        {
            Key = $"SP-{totalCount}",
            Title = command.Request.Title,
            Description = command.Request.Description,
            Type = command.Request.Type,
            Priority = command.Request.Priority,
            StoryPoints = command.Request.StoryPoints,
            EstimatedHours = command.Request.EstimatedHours,
            AssigneeId = command.Request.AssigneeId,
            SprintId = command.Request.SprintId,
            PrNumber = command.Request.PrNumber,
            PrUrl = command.Request.PrUrl,
            PrBranch = command.Request.PrBranch,
            TargetBranch = command.Request.TargetBranch ?? "main",
            Status = WorkItemStatus.Backlog
        };

        await repo.AddAsync(workItem, ct);
        await unitOfWork.CommitAsync(ct);

        string? assigneeName = null;
        if (workItem.AssigneeId.HasValue)
        {
            var member = await unitOfWork.Repository<TeamMember>().GetByIdAsync(workItem.AssigneeId.Value, ct);
            assigneeName = member?.Name;
        }

        return new WorkItemDto(
            workItem.Id, workItem.Key, workItem.Title, workItem.Description,
            workItem.Type, workItem.Status, workItem.Priority, workItem.StoryPoints,
            workItem.AssigneeId, assigneeName, workItem.SprintId,
            workItem.PrNumber, workItem.PrUrl, workItem.PrBranch, workItem.TargetBranch,
            null, null,
            workItem.CreatedAtUtc, null, null, null, null, null, null,
            workItem.DorAcceptanceCriteriaDefined, workItem.DorDependenciesIdentified,
            workItem.DorWireframeAvailable, workItem.DodUnitTestsPassed,
            workItem.DodPeerReviewCompleted, workItem.DodMergedToMaster,
            workItem.DodStagingVerified, false, null,
            null, null, null, null, null, null,
            workItem.EstimatedHours
        );
    }
}

public record AdvanceWorkItemStageCommand(Guid WorkItemId, AdvanceStageRequest Request) : ICommand<WorkItemDto>;

public class AdvanceWorkItemStageCommandHandler(
    IUnitOfWork unitOfWork,
    WorkItemCompletionSaga completionSaga
) : ICommandHandler<AdvanceWorkItemStageCommand, WorkItemDto>
{
    public async Task<WorkItemDto> HandleAsync(AdvanceWorkItemStageCommand command, CancellationToken ct = default)
    {
        // If advancing to "Done", execute the distributed Saga!
        if (command.Request.TargetStatus == WorkItemStatus.Done)
        {
            var sagaContext = new WorkItemCompletionContext
            {
                WorkItemId = command.WorkItemId,
                CustomTimestampUtc = command.Request.CustomTimestampUtc,
                ReviewerId = command.Request.ReviewerId
            };

            var sagaResult = await completionSaga.ExecuteAsync(sagaContext, ct);
            if (!sagaResult.IsSuccessful)
            {
                throw new InvalidOperationException(sagaResult.ErrorMessage);
            }

            return sagaResult.Result!;
        }

        // Standard stage progression with domain events
        var repo = unitOfWork.Repository<WorkItem>();
        var workItem = await repo.FirstOrDefaultAsync(new WorkItemWithRelationsByIdSpecification(command.WorkItemId), ct);
        if (workItem == null) throw new KeyNotFoundException($"WorkItem {command.WorkItemId} not found");

        var prevStatus = workItem.Status;
        var timestamp = command.Request.CustomTimestampUtc ?? DateTime.UtcNow;
        workItem.Status = command.Request.TargetStatus;

        switch (command.Request.TargetStatus)
        {
            case WorkItemStatus.InProgress when !workItem.PickedUpAtUtc.HasValue:
                workItem.PickedUpAtUtc = timestamp;
                break;
            case WorkItemStatus.PrCreated when !workItem.PrCreatedAtUtc.HasValue:
                workItem.PrCreatedAtUtc = timestamp;
                workItem.PrNumber = command.Request.PrNumber ?? workItem.PrNumber ?? "#101";
                workItem.PrUrl = command.Request.PrUrl ?? workItem.PrUrl ?? "https://github.com/ScrumPulse/pulls/101";
                workItem.PrBranch = workItem.PrBranch ?? $"feature/{workItem.Key.ToLower()}";
                break;
            case WorkItemStatus.PrApproved when !workItem.PrApprovedAtUtc.HasValue:
                workItem.PrApprovedAtUtc = timestamp;
                if (command.Request.ReviewerId.HasValue) workItem.PrReviewerId = command.Request.ReviewerId;
                break;
            case WorkItemStatus.Merged when !workItem.PrMergedAtUtc.HasValue:
                workItem.PrMergedAtUtc = timestamp;
                workItem.DodMergedToMaster = true;
                break;
            case WorkItemStatus.InQa when !workItem.QaStartedAtUtc.HasValue:
                workItem.QaStartedAtUtc = timestamp;
                break;
        }

        workItem.AddDomainEvent(new WorkItemStageAdvancedEvent(workItem.Id, workItem.Key, prevStatus, workItem.Status, workItem.AssigneeId));
        await unitOfWork.CommitAsync(ct);

        return new WorkItemDto(
            workItem.Id, workItem.Key, workItem.Title, workItem.Description,
            workItem.Type, workItem.Status, workItem.Priority, workItem.StoryPoints,
            workItem.AssigneeId, workItem.Assignee?.Name, workItem.SprintId,
            workItem.PrNumber, workItem.PrUrl, workItem.PrBranch, workItem.TargetBranch,
            workItem.PrReviewerId, workItem.PrReviewer?.Name,
            workItem.CreatedAtUtc, workItem.PickedUpAtUtc, workItem.PrCreatedAtUtc,
            workItem.PrApprovedAtUtc, workItem.PrMergedAtUtc, workItem.QaStartedAtUtc,
            workItem.CompletedAtUtc,
            workItem.DorAcceptanceCriteriaDefined, workItem.DorDependenciesIdentified,
            workItem.DorWireframeAvailable, workItem.DodUnitTestsPassed,
            workItem.DodPeerReviewCompleted, workItem.DodMergedToMaster,
            workItem.DodStagingVerified, workItem.IsEscapedDefect, workItem.DefectRootCause,
            workItem.PickupLatencyHours, workItem.DevCycleTimeHours,
            workItem.PrReviewLatencyHours, workItem.PrMergeLatencyHours,
            workItem.QaTestingLatencyHours, workItem.TotalCycleTimeHours,
            workItem.EstimatedHours
        );
    }
}
