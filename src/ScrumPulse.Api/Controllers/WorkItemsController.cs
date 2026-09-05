namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.CQRS;
using ScrumPulse.Application.CQRS.WorkItems;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Enums;

/// <summary>Work item management with CQRS for mutations and centralized DTO mapping.</summary>
public class WorkItemsController(
    IMediator mediator,
    IIdempotencyStore idempotencyStore,
    IAppDbContext db,
    ILogger<WorkItemsController>? logger = null
) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<WorkItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<WorkItemDto>>> GetAll(
        [FromQuery] Guid? sprintId, [FromQuery] WorkItemStatus? status, CancellationToken ct) =>
        Ok(await mediator.QueryAsync(new GetWorkItemsQuery(sprintId, status), ct));

    [HttpPost]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<WorkItemDto>> Create(
        [FromBody] CreateWorkItemRequest request,
        [FromHeader(Name = "X-Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await idempotencyStore.GetResponseAsync<WorkItemDto>(idempotencyKey, ct);
            if (cached != null) return Ok(cached);
        }

        var result = await mediator.SendAsync(new CreateWorkItemCommand(request), ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await idempotencyStore.SaveResponseAsync(idempotencyKey, result, null, ct);
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemDto>> Update(Guid id, [FromBody] UpdateWorkItemRequest request, CancellationToken ct)
    {
        var workItem = await db.WorkItems
            .Include(item => item.Assignee)
            .Include(item => item.PrReviewer)
            .FirstOrDefaultAsync(item => item.Id == id, ct);

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

        await db.SaveChangesAsync(ct);

        // Re fetch with includes to ensure accurate DTO mapping
        var updated = await db.WorkItems
            .Include(item => item.Assignee)
            .Include(item => item.PrReviewer)
            .FirstAsync(item => item.Id == id, ct);

        return Ok(updated.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workItem = await db.WorkItems.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (workItem == null) return NotFound();

        db.WorkItems.Remove(workItem);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/advance")]
    [HttpPost("{id:guid}/advance-stage")]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WorkItemDto>> AdvanceStage(Guid id, [FromBody] AdvanceStageRequest request, CancellationToken ct)
    {
        try
        {
            var result = await mediator.SendAsync(new AdvanceWorkItemStageCommand(id, request), ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            logger?.LogWarning(ex, "Work item {Id} not found when advancing stage", id);
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            logger?.LogWarning(ex, "Invalid operation when advancing work item {Id} stage: {Message}", id, ex.Message);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/quality-gates")]
    [ProducesResponseType(typeof(WorkItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WorkItemDto>> UpdateQualityGates(Guid id, [FromBody] UpdateQualityGatesRequest request, CancellationToken ct)
    {
        var workItem = await db.WorkItems.Include(item => item.Assignee).Include(item => item.PrReviewer)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (workItem == null) return NotFound();

        workItem.DorAcceptanceCriteriaDefined = request.DorAcceptanceCriteria;
        workItem.DorDependenciesIdentified = request.DorDependencies;
        workItem.DorWireframeAvailable = request.DorWireframe;
        workItem.DodUnitTestsPassed = request.DodUnitTests;
        workItem.DodPeerReviewCompleted = request.DodPeerReview;
        workItem.DodMergedToMaster = request.DodMergedToMaster;
        workItem.DodStagingVerified = request.DodStagingVerified;

        await db.SaveChangesAsync(ct);

        return Ok(workItem.ToDto());
    }
}
