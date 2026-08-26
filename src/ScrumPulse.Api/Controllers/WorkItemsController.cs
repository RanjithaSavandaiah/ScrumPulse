namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Enums;

public class WorkItemsController(
    IMediator mediator,
    IIdempotencyStore idempotencyStore,
    IAppDbContext db
) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAll([FromQuery] Guid? sprintId, [FromQuery] WorkItemStatus? status) =>
        Ok(await mediator.QueryAsync(new GetWorkItemsQuery(sprintId, status)));

    [HttpPost]
    public async Task<ActionResult<WorkItemDto>> Create(
        [FromBody] CreateWorkItemRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await idempotencyStore.GetResponseAsync<WorkItemDto>(idempotencyKey);
            if (cached != null) return Ok(cached);
        }

        var result = await mediator.SendAsync(new CreateWorkItemCommand(request));

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotencyStore.SaveResponseAsync(idempotencyKey, result);
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<WorkItemDto>> Update(Guid id, [FromBody] UpdateWorkItemRequest request)
    {
        var workItem = await db.WorkItems
            .Include(item => item.Assignee)
            .Include(item => item.PrReviewer)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (workItem == null) return NotFound();

        workItem.Title = request.Title;
        workItem.Description = request.Description;
        workItem.Type = request.Type;
        workItem.Priority = request.Priority;
        workItem.StoryPoints = request.StoryPoints;
        workItem.EstimatedHours = request.EstimatedHours;
        workItem.AssigneeId = request.AssigneeId;
        workItem.SprintId = request.SprintId;
        if (request.PrNumber != null) workItem.PrNumber = request.PrNumber;
        if (request.PrUrl != null) workItem.PrUrl = request.PrUrl;
        if (request.PrBranch != null) workItem.PrBranch = request.PrBranch;
        if (request.TargetBranch != null) workItem.TargetBranch = request.TargetBranch;
        workItem.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync();

        var updated = await db.WorkItems
            .Include(item => item.Assignee)
            .Include(item => item.PrReviewer)
            .FirstAsync(item => item.Id == id);

        return Ok(new WorkItemDto(
            updated.Id, updated.Key, updated.Title, updated.Description,
            updated.Type, updated.Status, updated.Priority, updated.StoryPoints,
            updated.AssigneeId, updated.Assignee?.Name, updated.SprintId,
            updated.PrNumber, updated.PrUrl, updated.PrBranch, updated.TargetBranch,
            updated.PrReviewerId, updated.PrReviewer?.Name,
            updated.CreatedAtUtc, updated.PickedUpAtUtc, updated.PrCreatedAtUtc,
            updated.PrApprovedAtUtc, updated.PrMergedAtUtc, updated.QaStartedAtUtc,
            updated.CompletedAtUtc,
            updated.DorAcceptanceCriteriaDefined, updated.DorDependenciesIdentified,
            updated.DorWireframeAvailable, updated.DodUnitTestsPassed,
            updated.DodPeerReviewCompleted, updated.DodMergedToMaster,
            updated.DodStagingVerified, updated.IsEscapedDefect, updated.DefectRootCause,
            updated.PickupLatencyHours, updated.DevCycleTimeHours,
            updated.PrReviewLatencyHours, updated.PrMergeLatencyHours,
            updated.QaTestingLatencyHours, updated.TotalCycleTimeHours,
            updated.EstimatedHours
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(item => item.Id == id);
        if (workItem == null) return NotFound();

        db.WorkItems.Remove(workItem);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:guid}/advance")]
    [HttpPost("{id:guid}/advance-stage")]
    public async Task<ActionResult<WorkItemDto>> AdvanceStage(Guid id, [FromBody] AdvanceStageRequest request)
    {
        try
        {
            var result = await mediator.SendAsync(new AdvanceWorkItemStageCommand(id, request));
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/quality-gates")]
    public async Task<ActionResult<WorkItemDto>> UpdateQualityGates(Guid id, [FromBody] UpdateQualityGatesRequest request)
    {
        var workItem = await db.WorkItems.Include(item => item.Assignee).Include(item => item.PrReviewer).FirstOrDefaultAsync(item => item.Id == id);
        if (workItem == null) return NotFound();

        workItem.DorAcceptanceCriteriaDefined = request.DorAcceptanceCriteria;
        workItem.DorDependenciesIdentified = request.DorDependencies;
        workItem.DorWireframeAvailable = request.DorWireframe;
        workItem.DodUnitTestsPassed = request.DodUnitTests;
        workItem.DodPeerReviewCompleted = request.DodPeerReview;
        workItem.DodMergedToMaster = request.DodMergedToMaster;
        workItem.DodStagingVerified = request.DodStagingVerified;

        await db.SaveChangesAsync();

        return Ok(new WorkItemDto(
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
        ));
    }
}
