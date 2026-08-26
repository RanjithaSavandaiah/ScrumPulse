namespace ScrumPulse.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using ScrumPulse.Application.DTOs;
using ScrumPulse.Application.Services;

public class AiCoachController(IAiAgentService aiAgentService) : BaseApiController
{
    [HttpGet("individual/{memberId:guid}")]
    public async Task<ActionResult<AiSuggestionResponse>> GetIndividual(Guid memberId) =>
        Ok(await aiAgentService.GenerateIndividualCoachingAsync(memberId));

    [HttpGet("project/{sprintId:guid}")]
    public async Task<ActionResult<AiSuggestionResponse>> GetProject(Guid sprintId) =>
        Ok(await aiAgentService.GenerateProjectSprintInsightsAsync(sprintId));

    [HttpGet("company")]
    public async Task<ActionResult<AiSuggestionResponse>> GetCompany() =>
        Ok(await aiAgentService.GenerateCompanyStrategicInsightsAsync());

    [HttpPost("suggest")]
    public async Task<ActionResult<AiSuggestionResponse>> GenerateSuggestions([FromBody] GenerateAiSuggestionsRequest request)
    {
        if (request.Level == "Individual" && request.TeamMemberId.HasValue)
        {
            return Ok(await aiAgentService.GenerateIndividualCoachingAsync(request.TeamMemberId.Value));
        }
        if (request.Level == "Project" && request.SprintId.HasValue)
        {
            return Ok(await aiAgentService.GenerateProjectSprintInsightsAsync(request.SprintId.Value));
        }
        return Ok(await aiAgentService.GenerateCompanyStrategicInsightsAsync());
    }

    [HttpPost("chat")]
    [HttpPost("ask")]
    public async Task<ActionResult<CopilotChatResponse>> Chat([FromBody] CopilotChatRequest request) =>
        Ok(await aiAgentService.ProcessCopilotChatAsync(request));
}
