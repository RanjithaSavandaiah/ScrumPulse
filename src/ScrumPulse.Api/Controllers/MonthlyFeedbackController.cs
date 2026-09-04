namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Mapping;
using ScrumPulse.Domain.Entities;

[Route("api/[controller]")]
[Route("api/feedback")]
[Route("api/monthly-feedback")]
[Route("api/monthlyfeedback")]
/// <summary>Monthly 1:1 feedback management with AI-synthesized insights.</summary>
public class MonthlyFeedbackController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MonthlyFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MonthlyFeedbackDto>>> GetAll([FromQuery] Guid? memberId, CancellationToken ct)
    {
        var query = db.Monthly1on1Feedbacks.Include(feedback => feedback.TeamMember).AsQueryable();
        if (memberId.HasValue) query = query.Where(feedback => feedback.TeamMemberId == memberId.Value);

        var list = await query.OrderByDescending(feedback => feedback.CreatedAtUtc).AsNoTracking().ToListAsync(ct);
        return Ok(list.ToDtos());
    }

    [HttpPost]
    [ProducesResponseType(typeof(MonthlyFeedbackDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<MonthlyFeedbackDto>> Submit([FromBody] SubmitMonthlyFeedbackRequest request, CancellationToken ct)
    {
        var smRating = request.SmRating > 0 ? request.SmRating : 5;
        var happinessIndex = request.HappinessIndex > 0 ? request.HappinessIndex : 5;

        var feedback = new Monthly1on1Feedback
        {
            TeamMemberId = request.TeamMemberId,
            MonthYear = string.IsNullOrWhiteSpace(request.MonthYear) ? DateTime.UtcNow.ToString("yyyy-MM") : request.MonthYear,
            ScrumMasterFeedback = request.ScrumMasterFeedback?.Trim() ?? string.Empty,
            CdlFeedback = request.CdlFeedback?.Trim() ?? string.Empty,
            ClientFeedback = request.ClientFeedback?.Trim() ?? string.Empty,
            SelfReflection = request.SelfReflection?.Trim() ?? string.Empty,
            SmRating = smRating,
            HappinessIndex = happinessIndex,
            ActionItems = request.ActionItems?.Trim() ?? string.Empty,
            NextMonthGoals = request.NextMonthGoals?.Trim() ?? string.Empty,
            AiSynthesizedStrengths = string.Empty,
            AiGrowthRecommendations = string.Empty,
            AiBurnoutRiskAssessment = happinessIndex < 6 ? "Medium Risk" : "Low Risk"
        };

        db.Monthly1on1Feedbacks.Add(feedback);
        await db.SaveChangesAsync(ct);

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        feedback.TeamMember = member;

        return Ok(feedback.ToDto());
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MonthlyFeedbackDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonthlyFeedbackDto>> Update(Guid id, [FromBody] SubmitMonthlyFeedbackRequest request, CancellationToken ct)
    {
        var feedback = await db.Monthly1on1Feedbacks.Include(f => f.TeamMember).FirstOrDefaultAsync(f => f.Id == id, ct);
        if (feedback == null) return NotFound();

        var smRating = request.SmRating > 0 ? request.SmRating : 5;
        var happinessIndex = request.HappinessIndex > 0 ? request.HappinessIndex : 5;

        feedback.TeamMemberId = request.TeamMemberId;
        if (!string.IsNullOrWhiteSpace(request.MonthYear)) feedback.MonthYear = request.MonthYear;
        if (request.ScrumMasterFeedback != null) feedback.ScrumMasterFeedback = request.ScrumMasterFeedback.Trim();
        if (request.CdlFeedback != null) feedback.CdlFeedback = request.CdlFeedback.Trim();
        if (request.ClientFeedback != null) feedback.ClientFeedback = request.ClientFeedback.Trim();
        if (request.SelfReflection != null) feedback.SelfReflection = request.SelfReflection.Trim();
        feedback.SmRating = smRating;
        feedback.HappinessIndex = happinessIndex;
        if (request.ActionItems != null) feedback.ActionItems = request.ActionItems.Trim();
        if (request.NextMonthGoals != null) feedback.NextMonthGoals = request.NextMonthGoals.Trim();
        feedback.AiBurnoutRiskAssessment = happinessIndex < 6 ? "Medium Risk" : "Low Risk";

        await db.SaveChangesAsync(ct);

        if (feedback.TeamMember == null)
        {
            feedback.TeamMember = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId, ct);
        }

        return Ok(feedback.ToDto());
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var feedback = await db.Monthly1on1Feedbacks.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (feedback == null) return NotFound();
        db.Monthly1on1Feedbacks.Remove(feedback);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
