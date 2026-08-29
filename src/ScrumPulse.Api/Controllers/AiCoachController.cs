namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

/// <summary>AI coaching and copilot chat with rate limiting for token budget protection.</summary>
[EnableRateLimiting("ai")]
public class AiCoachController(IAiAgentService aiAgentService) : BaseApiController
{
    [HttpGet("individual/{memberId:guid}")]
    [ProducesResponseType(typeof(AiSuggestionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiSuggestionResponse>> GetIndividual(Guid memberId, CancellationToken ct) =>
        Ok(await aiAgentService.GenerateIndividualCoachingAsync(memberId, ct));

    [HttpGet("project/{sprintId:guid}")]
    [ProducesResponseType(typeof(AiSuggestionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiSuggestionResponse>> GetProject(Guid sprintId, CancellationToken ct) =>
        Ok(await aiAgentService.GenerateProjectSprintInsightsAsync(sprintId, ct));

    [HttpGet("company")]
    [ProducesResponseType(typeof(AiSuggestionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiSuggestionResponse>> GetCompany(CancellationToken ct) =>
        Ok(await aiAgentService.GenerateCompanyStrategicInsightsAsync(ct));

    [HttpPost("suggest")]
    [ProducesResponseType(typeof(AiSuggestionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AiSuggestionResponse>> GenerateSuggestions([FromBody] GenerateAiSuggestionsRequest request, CancellationToken ct)
    {
        if (request.Level == "Individual" && request.TeamMemberId.HasValue)
        {
            return Ok(await aiAgentService.GenerateIndividualCoachingAsync(request.TeamMemberId.Value, ct));
        }
        if (request.Level == "Project" && request.SprintId.HasValue)
        {
            return Ok(await aiAgentService.GenerateProjectSprintInsightsAsync(request.SprintId.Value, ct));
        }
        return Ok(await aiAgentService.GenerateCompanyStrategicInsightsAsync(ct));
    }

    [HttpPost("chat")]
    [HttpPost("ask")]
    [ProducesResponseType(typeof(CopilotChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CopilotChatResponse>> Chat([FromBody] CopilotChatRequest request, CancellationToken ct) =>
        Ok(await aiAgentService.ProcessCopilotChatAsync(request, ct));
}
