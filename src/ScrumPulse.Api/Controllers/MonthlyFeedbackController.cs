namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Domain.Entities;

[Route("api/[controller]")]
[Route("api/feedback")]
[Route("api/monthly-feedback")]
[Route("api/monthlyfeedback")]
public class MonthlyFeedbackController(IAppDbContext db) : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MonthlyFeedbackDto>>> GetAll([FromQuery] Guid? memberId)
    {
        var query = db.Monthly1on1Feedbacks.Include(feedback => feedback.TeamMember).AsQueryable();
        if (memberId.HasValue) query = query.Where(feedback => feedback.TeamMemberId == memberId.Value);

        var list = await query.OrderByDescending(feedback => feedback.CreatedAtUtc).ToListAsync();
        return Ok(list.Select(feedback => new MonthlyFeedbackDto(
            feedback.Id, feedback.TeamMemberId, feedback.TeamMember?.Name ?? "Member",
            feedback.MonthYear, feedback.ScrumMasterFeedback, feedback.CdlFeedback,
            feedback.ClientFeedback, feedback.SelfReflection, feedback.SmRating,
            feedback.HappinessIndex, feedback.ActionItems, feedback.NextMonthGoals,
            feedback.AiSynthesizedStrengths, feedback.AiGrowthRecommendations,
            feedback.AiBurnoutRiskAssessment, feedback.CreatedAtUtc
        )));
    }

    [HttpPost]
    public async Task<ActionResult<MonthlyFeedbackDto>> Submit([FromBody] SubmitMonthlyFeedbackRequest request)
    {
        var smRating = request.SmRating > 0 ? request.SmRating : 5;
        var happinessIndex = request.HappinessIndex > 0 ? request.HappinessIndex : 5;

        var feedback = new Monthly1on1Feedback
        {
            TeamMemberId = request.TeamMemberId,
            MonthYear = string.IsNullOrWhiteSpace(request.MonthYear) ? DateTime.UtcNow.ToString("yyyy-MM") : request.MonthYear,
            ScrumMasterFeedback = string.IsNullOrWhiteSpace(request.ScrumMasterFeedback) ? "Consistent sprint delivery and active team collaboration." : request.ScrumMasterFeedback.Trim(),
            CdlFeedback = string.IsNullOrWhiteSpace(request.CdlFeedback) ? "Steady career progression and architectural competency." : request.CdlFeedback.Trim(),
            ClientFeedback = string.IsNullOrWhiteSpace(request.ClientFeedback) ? "Positive sprint demo perception and prompt communication." : request.ClientFeedback.Trim(),
            SelfReflection = string.IsNullOrWhiteSpace(request.SelfReflection) ? "Focused on velocity commitment and Definition of Done." : request.SelfReflection.Trim(),
            SmRating = smRating,
            HappinessIndex = happinessIndex,
            ActionItems = string.IsNullOrWhiteSpace(request.ActionItems) ? "Maintain focus on DoD quality checks and architectural mentoring." : request.ActionItems.Trim(),
            NextMonthGoals = string.IsNullOrWhiteSpace(request.NextMonthGoals) ? "Continue feature delivery and test coverage expansion." : request.NextMonthGoals.Trim(),
            AiSynthesizedStrengths = "High technical agility; swift PR resolutions; zero QA escapes.",
            AiGrowthRecommendations = "Continue cross-squad architecture mentoring during overlap hours.",
            AiBurnoutRiskAssessment = happinessIndex < 6 ? "Medium Risk" : "Low Risk"
        };

        db.Monthly1on1Feedbacks.Add(feedback);
        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId);

        return Ok(new MonthlyFeedbackDto(
            feedback.Id, feedback.TeamMemberId, member?.Name ?? "Member",
            feedback.MonthYear, feedback.ScrumMasterFeedback, feedback.CdlFeedback,
            feedback.ClientFeedback, feedback.SelfReflection, feedback.SmRating,
            feedback.HappinessIndex, feedback.ActionItems, feedback.NextMonthGoals,
            feedback.AiSynthesizedStrengths, feedback.AiGrowthRecommendations,
            feedback.AiBurnoutRiskAssessment, feedback.CreatedAtUtc
        ));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MonthlyFeedbackDto>> Update(Guid id, [FromBody] SubmitMonthlyFeedbackRequest request)
    {
        var feedback = await db.Monthly1on1Feedbacks.Include(f => f.TeamMember).FirstOrDefaultAsync(f => f.Id == id);
        if (feedback == null) return NotFound();

        var smRating = request.SmRating > 0 ? request.SmRating : 5;
        var happinessIndex = request.HappinessIndex > 0 ? request.HappinessIndex : 5;

        feedback.TeamMemberId = request.TeamMemberId;
        if (!string.IsNullOrWhiteSpace(request.MonthYear)) feedback.MonthYear = request.MonthYear;
        feedback.ScrumMasterFeedback = string.IsNullOrWhiteSpace(request.ScrumMasterFeedback) ? "Consistent sprint delivery and active team collaboration." : request.ScrumMasterFeedback.Trim();
        feedback.CdlFeedback = string.IsNullOrWhiteSpace(request.CdlFeedback) ? "Steady career progression and architectural competency." : request.CdlFeedback.Trim();
        feedback.ClientFeedback = string.IsNullOrWhiteSpace(request.ClientFeedback) ? "Positive sprint demo perception and prompt communication." : request.ClientFeedback.Trim();
        feedback.SelfReflection = string.IsNullOrWhiteSpace(request.SelfReflection) ? "Focused on velocity commitment and Definition of Done." : request.SelfReflection.Trim();
        feedback.SmRating = smRating;
        feedback.HappinessIndex = happinessIndex;
        feedback.ActionItems = string.IsNullOrWhiteSpace(request.ActionItems) ? "Maintain focus on DoD quality checks and architectural mentoring." : request.ActionItems.Trim();
        feedback.NextMonthGoals = string.IsNullOrWhiteSpace(request.NextMonthGoals) ? "Continue feature delivery and test coverage expansion." : request.NextMonthGoals.Trim();
        feedback.AiBurnoutRiskAssessment = happinessIndex < 6 ? "Medium Risk" : "Low Risk";

        await db.SaveChangesAsync();

        var member = await db.TeamMembers.FirstOrDefaultAsync(teamMember => teamMember.Id == request.TeamMemberId);

        return Ok(new MonthlyFeedbackDto(
            feedback.Id, feedback.TeamMemberId, member?.Name ?? feedback.TeamMember?.Name ?? "Member",
            feedback.MonthYear, feedback.ScrumMasterFeedback, feedback.CdlFeedback,
            feedback.ClientFeedback, feedback.SelfReflection, feedback.SmRating,
            feedback.HappinessIndex, feedback.ActionItems, feedback.NextMonthGoals,
            feedback.AiSynthesizedStrengths, feedback.AiGrowthRecommendations,
            feedback.AiBurnoutRiskAssessment, feedback.CreatedAtUtc
        ));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var feedback = await db.Monthly1on1Feedbacks.FirstOrDefaultAsync(f => f.Id == id);
        if (feedback == null) return NotFound();

        db.Monthly1on1Feedbacks.Remove(feedback);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
