namespace ScrumPulse.Application.DTOs;

public record AiSuggestionResponse(
    string Level, // Individual, Project, Company
    string Title,
    string Summary,
    List<string> KeyFindings,
    List<string> ActionableRecommendations,
    string RiskLevel, // Low, Medium, High, Critical
    DateTime GeneratedAtUtc
);

public record CopilotChatRequest(string Prompt, string? RoleContext = "ScrumMaster", Guid? SprintId = null);
public record CopilotChatResponse(string Answer, List<string> SuggestedFollowUps, DateTime TimestampUtc);
public record GenerateAiSuggestionsRequest(string Level = "Company", Guid? TeamMemberId = null, Guid? SprintId = null);
