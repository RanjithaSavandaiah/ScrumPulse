namespace ScrumPulse.Application.Services;
using ScrumPulse.Application.DTOs;

public interface IAiAgentService
{
    Task<AiSuggestionResponse> GenerateIndividualCoachingAsync(Guid memberId, CancellationToken ct = default);
    Task<AiSuggestionResponse> GenerateProjectSprintInsightsAsync(Guid sprintId, CancellationToken ct = default);
    Task<AiSuggestionResponse> GenerateCompanyStrategicInsightsAsync(CancellationToken ct = default);
    Task<CopilotChatResponse> ProcessCopilotChatAsync(CopilotChatRequest request, CancellationToken ct = default);
}
